using System.Buffers.Binary;
using System.Net.Sockets;
using HrDeviceGateway.Models;

namespace HrDeviceGateway.Protocol;

/// <summary>
/// کلاینت TCP باینری ZKTeco (پورت 4370) — پورت وفادار pyzk/zkemkeeper با قالب TCP استاندارد:
/// [50 50 82 7d][len:4 LE][cmd:2][chk:2][sid:2][rid:2][data]
/// پشتیبانی کامل: اتصال/احراز، کاربران، رکوردهای تردد، ساعت، درب، ری‌استارت، پاک‌سازی و…
/// </summary>
public sealed class ZkTcpClient : IDisposable
{
    private readonly int _timeoutMs;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private ushort _sessionId;
    private ushort _replyId = ZkConsts.UshrtMax - 1; // 65534 — مطابق pyzk
    private bool _connected;

    public string Remote { get; private set; } = "";
    public bool IsConnected => _connected;

    public ZkTcpClient(int timeoutMs = 6000) => _timeoutMs = timeoutMs;

    // ================= اتصال =================

    /// <summary>اتصال TCP خام به دستگاه.</summary>
    public async Task ConnectAsync(string ip, int port, CancellationToken ct = default)
    {
        Remote = $"{ip}:{port}";
        _tcp = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeoutMs);
        await _tcp.ConnectAsync(ip, port, cts.Token);
        _stream = _tcp.GetStream();
        _sessionId = 0;
        _replyId = ZkConsts.UshrtMax - 1;
        _connected = true;
    }

    /// <summary>اتصال کامل با احراز (CMD_CONNECT و در صورت نیاز CMD_AUTH با CommKey).</summary>
    public async Task LoginAsync(int commKey = 0, CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdConnect, null, ct);
        _sessionId = r.SessionId;
        if (r.Cmd == ZkConsts.CmdAckUnauth)
        {
            byte[] key = ZkCodec.MakeCommKey((uint)Math.Max(0, commKey), _sessionId);
            r = await ExecAsync(ZkConsts.CmdAuth, key, ct);
            if (r.Cmd != ZkConsts.CmdAckOk)
                throw new ZkException("کلید ارتباطی (CommKey) پذیرفته نشد");
        }
        else if (r.Cmd != ZkConsts.CmdAckOk)
        {
            throw new ZkException($"پاسخ اتصال نامعتبر: {r.Cmd}");
        }
    }

    /// <summary>قطع تمیز اتصال (CMD_EXIT).</summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        try
        {
            if (_connected) await ExecAsync(ZkConsts.CmdExit, null, ct);
        }
        catch { /* دستگاه ممکن است بسته باشد */ }
        finally { Dispose(); }
    }

    public void Dispose()
    {
        try { _stream?.Close(); } catch { }
        try { _tcp?.Close(); } catch { }
        _stream = null;
        _tcp = null;
        _connected = false;
    }

    // ================= لایه پایین =================

    public sealed record ZkReply(ushort Cmd, ushort SessionId, ushort ReplyId, byte[] Data);

    /// <summary>خواندن یک فریم کامل TCP (بر اساس فیلد طول — مقاوم به تکه‌تکه شدن).</summary>
    private async Task<byte[]> ReadFrameAsync(CancellationToken ct)
    {
        byte[] header = await ReadExactAsync(8, ct);
        uint length = ZkCodec.TcpTopLength(header);
        if (length < 8 || length > 32 * 1024 * 1024) throw new ZkException("فریم TCP نامعتبر (سرصفحه ZK نیست)");
        byte[] body = await ReadExactAsync((int)length, ct);
        return body; // cmd(2) chk(2) sid(2) rid(2) data...
    }

    private async Task<byte[]> ReadExactAsync(int count, CancellationToken ct)
    {
        if (_stream is null) throw new ZkException("اتصال برقرار نیست");
        byte[] buf = new byte[count];
        int read = 0;
        while (read < count)
        {
            int n = await _stream.ReadAsync(buf.AsMemory(read, count - read), ct);
            if (n <= 0) throw new ZkException("اتصال دستگاه قطع شد");
            read += n;
        }
        return buf;
    }

    /// <summary>ارسال فرمان و دریافت پاسخ — پورت __send_command در pyzk.</summary>
    public async Task<ZkReply> ExecAsync(ushort cmd, byte[]? data = null, CancellationToken ct = default)
    {
        if (_stream is null) throw new ZkException("اتصال برقرار نیست");

        byte[] pkt = ZkCodec.BuildPacket(cmd, data ?? Array.Empty<byte>(), _sessionId, _replyId, out ushort nextRid);
        byte[] frame = ZkCodec.Frame(pkt);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeoutMs);
        await _stream.WriteAsync(frame, cts.Token);

        byte[] body = await ReadFrameAsync(cts.Token);
        ushort rcmd = BinaryPrimitives.ReadUInt16LittleEndian(body);
        ushort rsid = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(4));
        ushort rrid = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(6));
        byte[] rdata = body.Length > 8 ? body[8..] : Array.Empty<byte>();

        _sessionId = rsid;
        _replyId = rrid; // دستگاه همان مقدار ارسالی را برمی‌گرداند
        return new ZkReply(rcmd, rsid, rrid, rdata);
    }

    private static void EnsureOk(ZkReply r, string action)
    {
        if (r.Cmd != ZkConsts.CmdAckOk && r.Cmd != ZkConsts.CmdAckData && r.Cmd != ZkConsts.CmdData)
            throw new ZkException($"{action} ناموفق (کد {r.Cmd})");
    }

    // ================= اطلاعات پایه =================

    public async Task<DateTime> GetTimeAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdGetTime, null, ct);
        EnsureOk(r, "خواندن ساعت");
        return r.Data.Length >= 4 ? ZkCodec.DecodeTime(r.Data) : throw new ZkException("داده ساعت کوتاه است");
    }

    public async Task SetTimeAsync(DateTime? dt = null, CancellationToken ct = default)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, ZkCodec.EncodeTime(dt ?? DateTime.Now));
        var r = await ExecAsync(ZkConsts.CmdSetTime, payload, ct);
        EnsureOk(r, "تنظیم ساعت");
    }

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdGetVersion, null, ct);
        EnsureOk(r, "خواندن نسخه");
        return ZkCodec.DecodeString(r.Data, 0, r.Data.Length).Trim('\0');
    }

    /// <summary>خواندن یک پارامتر تنظیمات (CMD_OPTIONS_RRQ — الگوی ~SerialNumber).</summary>
    public async Task<string> GetOptionAsync(string key, CancellationToken ct = default)
    {
        byte[] payload = System.Text.Encoding.ASCII.GetBytes($"~{key}\0");
        var r = await ExecAsync(ZkConsts.CmdOptionsRrq, payload, ct);
        EnsureOk(r, $"خواندن تنظیم {key}");
        // پاسخ ممکن است "KEY=value" یا فقط value باشد
        string s = ZkCodec.DecodeString(r.Data, 0, r.Data.Length).Trim('\0');
        int eq = s.IndexOf('=');
        return eq >= 0 ? s[(eq + 1)..].Trim() : s.Trim();
    }

    public Task<string> GetSerialNumberAsync(CancellationToken ct = default) => GetOptionAsync("SerialNumber", ct);
    public Task<string> GetDeviceNameAsync(CancellationToken ct = default) => GetOptionAsync("DeviceName", ct);
    public Task<string> GetPlatformAsync(CancellationToken ct = default) => GetOptionAsync("Platform", ct);

    /// <summary>شمارش کاربران/رکوردها (CMD_GET_FREE_SIZES).</summary>
    public async Task<ZkSizes> ReadSizesAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdGetFreeSizes, null, ct);
        EnsureOk(r, "خواندن ظرفیت");
        if (r.Data.Length < 80) throw new ZkException("پاسخ ظرفیت کوتاه است");
        int Int32At(int idx) => BinaryPrimitives.ReadInt32LittleEndian(r.Data.AsSpan(idx * 4));
        return new ZkSizes(
            Users: Int32At(4), Fingers: Int32At(6), Records: Int32At(8),
            Cards: Int32At(12),
            UsersCap: Int32At(15), RecCap: Int32At(16));
    }

    // ================= کنترل دستگاه =================

    /// <summary>بازکردن درب — CMD_UNLOCK با مدت زمان × 10 (مطابق pyzk unlock).</summary>
    public async Task UnlockAsync(int seconds = 5, CancellationToken ct = default)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)(Math.Max(1, seconds) * 10));
        var r = await ExecAsync(ZkConsts.CmdUnlock, payload, ct);
        EnsureOk(r, "بازکردن درب");
    }

    public async Task RestartAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdRestart, null, ct);
        EnsureOk(r, "ری‌استارت");
    }

    public async Task PoweroffAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdPoweroff, null, ct);
        EnsureOk(r, "خاموش‌کردن");
    }

    public async Task RefreshDataAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdRefreshData, null, ct);
        EnsureOk(r, "نوسازی داده");
    }

    public async Task EnableDeviceAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdEnableDevice, null, ct);
        EnsureOk(r, "فعال‌سازی");
    }

    public async Task DisableDeviceAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdDisableDevice, null, ct);
        EnsureOk(r, "غیرفعال‌سازی");
    }

    public async Task TestVoiceAsync(int index = 0, CancellationToken ct = default)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)index);
        var r = await ExecAsync(ZkConsts.CmdTestVoice, payload, ct);
        EnsureOk(r, "پخش صدا");
    }

    public async Task ClearDataAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdClearData, null, ct);
        EnsureOk(r, "پاک‌سازی داده‌ها");
    }

    public async Task ClearAttendanceAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdClearAttlog, null, ct);
        EnsureOk(r, "پاک‌سازی رکوردها");
    }

    public async Task<int> GetStateAsync(CancellationToken ct = default)
    {
        var r = await ExecAsync(ZkConsts.CmdStateRrq, null, ct);
        EnsureOk(r, "خواندن وضعیت");
        return r.Data.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(r.Data) : -1;
    }

    /// <summary>ثبت رویدادهای لحظه‌ای (پایه live-capture) — پیش‌فرض: تردد.</summary>
    public async Task RegisterEventsAsync(uint flags = ZkConsts.EfAttlog, CancellationToken ct = default)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, flags);
        var r = await ExecAsync(ZkConsts.CmdRegEvent, payload, ct);
        EnsureOk(r, "ثبت رویدادها");
    }

    // ================= خواندن داده با بافر (الگوی PREPARE_BUFFER) =================

    /// <summary>
    /// خواندن داده‌های حجیم (کاربران/رکوردها) — پورت read_with_buffer در pyzk:
    /// CMD_PREPARE_BUFFER(1503) → CMD_PREPARE_DATA(حجم) → تکه‌خوانی با CMD_READ_BUFFER(1504) → CMD_FREE_DATA(1502)
    /// </summary>
    public async Task<byte[]> ReadWithBufferAsync(ushort command, byte fct = 0, int ext = 0,
                                                  CancellationToken ct = default)
    {
        const int MaxChunk = 0xFFc0; // مطابق pyzk حالت TCP

        byte[] cs = new byte[11]; // pack('<bhii', 1, command, fct, ext)
        cs[0] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(cs.AsSpan(1), command);
        BinaryPrimitives.WriteInt32LittleEndian(cs.AsSpan(3), fct);
        BinaryPrimitives.WriteInt32LittleEndian(cs.AsSpan(7), ext);

        var first = await ExecAsync(ZkConsts.CmdPrepareBuffer, cs, ct);
        if (first.Cmd == ZkConsts.CmdData)
            return first.Data; // داده کوچک — همان لحظه در پاسخ
        if (first.Cmd != ZkConsts.CmdPrepareData)
            throw new ZkException($"دستور بافر پشتیبانی نمی‌شود (کد {first.Cmd})");

        // حجم کل — مطابق pyzk: unpack('I', data[1:5])
        if (first.Data.Length < 5) throw new ZkException("حجم داده نامعتبر است");
        uint total = BinaryPrimitives.ReadUInt32LittleEndian(first.Data.AsSpan(1));

        using var ms = new MemoryStream();
        uint start = 0;
        while (start < total)
        {
            int chunk = (int)Math.Min(MaxChunk, total - start);
            byte[] part = await ReadChunkAsync(start, chunk, ct);
            await ms.WriteAsync(part, ct);
            start += (uint)chunk;
        }

        try { await ExecAsync(ZkConsts.CmdFreeData, null, ct); } catch { /* آزادسازی اختیاری */ }
        return ms.ToArray();
    }

    private async Task<byte[]> ReadChunkAsync(uint start, int size, CancellationToken ct)
    {
        byte[] cs = new byte[8]; // pack('<ii', start, size)
        BinaryPrimitives.WriteInt32LittleEndian(cs.AsSpan(0), (int)start);
        BinaryPrimitives.WriteInt32LittleEndian(cs.AsSpan(4), size);

        for (int retry = 0; retry < 3; retry++)
        {
            var r = await ExecAsync(ZkConsts.CmdReadBuffer, cs, ct);
            if (r.Cmd == ZkConsts.CmdData) return r.Data;
            if (r.Cmd == ZkConsts.CmdAckOk) return r.Data;
        }
        throw new ZkException($"خواندن تکه {start}:[{size}] ناموفق بود");
    }

    // ================= کاربران =================

    /// <summary>خواندن همه کاربران — پورت get_users در pyzk (قالب 28 و 72 بایتی).</summary>
    public async Task<List<ZkUser>> GetUsersAsync(CancellationToken ct = default)
    {
        var sizes = await ReadSizesAsync(ct);
        if (sizes.Users <= 0) return new List<ZkUser>();

        byte[] buf = await ReadWithBufferAsync(ZkConsts.CmdUserTempRrq, ZkConsts.FctUser, 0, ct);
        if (buf.Length <= 4) return new List<ZkUser>();

        uint totalSize = BinaryPrimitives.ReadUInt32LittleEndian(buf);
        byte[] data = buf[4..];
        int packetSize = (int)(totalSize / (uint)sizes.Users);
        if (packetSize != 28 && packetSize != 72) packetSize = 72; // پیش‌فرض zk8

        var users = new List<ZkUser>();
        if (packetSize == 28)
        {
            // '<HB5s8sIxBhI' — پیمایش 28 بایتی (27 بایت ساختار + 1 بایت پرشده)
            for (int off = 0; off + 28 <= data.Length; off += 28)
            {
                int uid = data[off] | (data[off + 1] << 8);
                int priv = data[off + 2];
                string password = ZkCodec.NullTerminated(data, off + 3, 5);
                string name = ZkCodec.NullTerminated(data, off + 8, 8);
                uint card = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 16));
                short group = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(off + 21));
                uint userIdRaw = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 24));
                string userId = userIdRaw.ToString();
                users.Add(new ZkUser(uid, string.IsNullOrEmpty(name) ? $"NN-{userId}" : name,
                                     priv, password, group.ToString(), userId, card));
            }
        }
        else
        {
            // '<HB8s24sIx7sx24s' — پیمایش 72 بایتی (71 بایت ساختار + 1 بایت پرشده)
            for (int off = 0; off + 72 <= data.Length; off += 72)
            {
                int uid = data[off] | (data[off + 1] << 8);
                int priv = data[off + 2];
                string password = ZkCodec.NullTerminated(data, off + 3, 8);
                string name = ZkCodec.NullTerminated(data, off + 11, 24);
                uint card = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 35));
                string group = ZkCodec.NullTerminated(data, off + 40, 7);
                string userId = ZkCodec.NullTerminated(data, off + 48, 24);
                users.Add(new ZkUser(uid, string.IsNullOrEmpty(name) ? $"NN-{userId}" : name,
                                     priv, password, group, userId, card));
            }
        }
        return users;
    }

    /// <summary>ایجاد/به‌روزرسانی کاربر — پورت set_user در pyzk (قالب 72 بایتی).</summary>
    public async Task SetUserAsync(int uid, string name, string userId, int privilege = ZkConsts.UserDefault,
                                   string password = "", string groupId = "0", uint card = 0,
                                   CancellationToken ct = default)
    {
        byte[] pkt = new byte[72];
        pkt[0] = (byte)(uid & 0xff);
        pkt[1] = (byte)((uid >> 8) & 0xff);
        pkt[2] = (byte)(privilege is ZkConsts.UserDefault or ZkConsts.UserAdmin ? privilege : ZkConsts.UserDefault);

        byte[] pwd = System.Text.Encoding.ASCII.GetBytes(password ?? "");
        Array.Copy(pwd, 0, pkt, 3, Math.Min(8, pwd.Length));

        byte[] nm = System.Text.Encoding.UTF8.GetBytes(name ?? "");
        Array.Copy(nm, 0, pkt, 11, Math.Min(24, nm.Length));

        BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(35), card);

        byte[] grp = System.Text.Encoding.ASCII.GetBytes(groupId ?? "0");
        Array.Copy(grp, 0, pkt, 40, Math.Min(7, grp.Length));

        byte[] uidStr = System.Text.Encoding.UTF8.GetBytes(userId ?? "");
        Array.Copy(uidStr, 0, pkt, 48, Math.Min(24, uidStr.Length));

        var r = await ExecAsync(ZkConsts.CmdUserWrq, pkt, ct);
        EnsureOk(r, "نوشتن کاربر");
        await RefreshDataAsync(ct);
    }

    /// <summary>حذف کاربر با uid — پورت delete_user در pyzk.</summary>
    public async Task DeleteUserByUidAsync(int uid, CancellationToken ct = default)
    {
        byte[] payload = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)uid);
        var r = await ExecAsync(ZkConsts.CmdDeleteUser, payload, ct);
        EnsureOk(r, "حذف کاربر");
        await RefreshDataAsync(ct);
    }

    /// <summary>حذف کاربر با کد پرسنلی (user_id) — با حل‌کردن uid از فهرست کاربران.</summary>
    public async Task<bool> DeleteUserByPinAsync(string pin, CancellationToken ct = default)
    {
        var users = await GetUsersAsync(ct);
        var u = users.FirstOrDefault(x => x.UserId == pin);
        if (u is null) return false;
        await DeleteUserByUidAsync(u.Uid, ct);
        return true;
    }

    // ================= قالب‌های هویتی (اثر انگشت/چهره) =================

    // قرارداد قالب‌خوانی/نوشتن (خانواده CMD_USERTEMP مطابق zkemsdk.c):
    //   خواندن:  CMD_USERTEMP_RRQ(9) با payload [uid:2][index:1][typeFlag:1]
    //            پاسخ CMD_ACK_DATA: [size:4][valid:1][dup:1][template bytes]
    //   نوشتن:  CMD_USERTEMP_WRQ(10) با payload [uid:2][index:1][typeFlag:1][valid:1][dup:1][size:4][template]
    // typeFlag: 0 = اثر انگشت (FP) ، 1 = چهره (Face)

    /// <summary>خواندن یک قالب هویتی کاربر — null اگر قالبی ثبت نشده باشد.</summary>
    public async Task<ZkBio?> GetUserTemplateAsync(int uid, string pin, int index, int typeFlag = 0,
                                                   CancellationToken ct = default)
    {
        byte[] payload =
        {
            (byte)(uid & 0xff),
            (byte)((uid >> 8) & 0xff),
            (byte)Math.Max(0, index),
            (byte)Math.Clamp(typeFlag, 0, 1),
        };
        var r = await ExecAsync(ZkConsts.CmdUserTempRrq, payload, ct);
        if (r.Cmd != ZkConsts.CmdAckData && r.Cmd != ZkConsts.CmdAckOk) return null;
        if (r.Data.Length < 6) return null;

        int size = BinaryPrimitives.ReadInt32LittleEndian(r.Data);
        if (size <= 0 || 4 + size > r.Data.Length) return null;
        byte valid = r.Data.Length > 4 ? r.Data[4] : (byte)1;
        byte[] tpl = r.Data[6..(4 + size)];
        return new ZkBio(uid, pin, index, valid == 1 ? 1 : 0, tpl);
    }

    /// <summary>نوشتن قالب هویتی روی دستگاه (ثبت اثر انگشت/چهره برای کاربر).</summary>
    public async Task SetUserTemplateAsync(int uid, string pin, int index, int valid, int typeFlag,
                                           byte[] template, CancellationToken ct = default)
    {
        if (template is null || template.Length == 0)
            throw new ZkException($"قالب هویتی کاربر {pin} خالی است");
        if (template.Length > 3 * 1024) // قالب FP/چهره معمولا زیر ۳ کیلوبایت است
            throw new ZkException($"قالب هویتی کاربر {pin} بیش از حد بزرگ است ({template.Length} بایت)");

        byte[] pkt = new byte[10 + template.Length];
        pkt[0] = (byte)(uid & 0xff);
        pkt[1] = (byte)((uid >> 8) & 0xff);
        pkt[2] = (byte)Math.Max(0, index);
        pkt[3] = (byte)Math.Clamp(typeFlag, 0, 1);
        pkt[4] = (byte)(valid == 1 ? 1 : 0);
        pkt[5] = 0; // dup
        BinaryPrimitives.WriteInt32LittleEndian(pkt.AsSpan(6), template.Length);
        Array.Copy(template, 0, pkt, 10, template.Length);

        var r = await ExecAsync(ZkConsts.CmdUserTempWrq, pkt, ct);
        EnsureOk(r, $"نوشتن قالب هویتی کاربر {pin}");
        await RefreshDataAsync(ct);
    }

    // ================= رکوردهای تردد =================

    /// <summary>خواندن همه رکوردهای تردد — پورت get_attendance در pyzk (قالب‌های 8/16/40 بایتی).</summary>
    public async Task<List<ZkAttendance>> GetAttendanceAsync(CancellationToken ct = default)
    {
        var sizes = await ReadSizesAsync(ct);
        if (sizes.Records <= 0) return new List<ZkAttendance>();

        byte[] buf = await ReadWithBufferAsync(ZkConsts.CmdAttlogRrq, 0, 0, ct);
        if (buf.Length <= 4) return new List<ZkAttendance>();

        uint totalSize = BinaryPrimitives.ReadUInt32LittleEndian(buf);
        byte[] data = buf[4..];
        int recordSize = (int)(totalSize / (uint)sizes.Records);
        if (recordSize is not (8 or 16 or 40)) recordSize = 16; // قالب رایج دستگاه‌های جدید

        var list = new List<ZkAttendance>();
        switch (recordSize)
        {
            case 8: // uid(H) status(B) ts(4) punch(B)
                for (int off = 0; off + 8 <= data.Length; off += 8)
                {
                    int uid = data[off] | (data[off + 1] << 8);
                    int status = data[off + 2];
                    var ts = ZkCodec.DecodeTime(data.AsSpan(off + 3, 4));
                    int punch = data[off + 7];
                    list.Add(new ZkAttendance(uid.ToString(), ts, status, punch));
                }
                break;

            case 16: // userId(I) ts(4) status(B) punch(B) res(2) workcode(I)
                for (int off = 0; off + 16 <= data.Length; off += 16)
                {
                    uint userId = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off));
                    var ts = ZkCodec.DecodeTime(data.AsSpan(off + 4, 4));
                    int status = data[off + 8];
                    int punch = data[off + 9];
                    list.Add(new ZkAttendance(userId.ToString(), ts, status, punch));
                }
                break;

            case 40: // uid(H) userId(24s) status(B) ts(4) punch(B) space(8s)
                for (int off = 0; off + 40 <= data.Length; off += 40)
                {
                    string userId = ZkCodec.NullTerminated(data, off + 2, 24);
                    int status = data[off + 26];
                    var ts = ZkCodec.DecodeTime(data.AsSpan(off + 27, 4));
                    int punch = data[off + 31];
                    list.Add(new ZkAttendance(userId, ts, status, punch));
                }
                break;
        }
        return list;
    }
}

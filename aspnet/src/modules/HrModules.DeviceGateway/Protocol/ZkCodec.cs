using System.Buffers.Binary;

// پورت‌شده از سرویس ویندوزی HrDeviceGateway (فاز ۶۳) — منطق دست‌نخورده
namespace HrModules.DeviceGateway.Protocol;

/// <summary>
/// کدک پروتکل ZK — پورت وفادار از zkemsdk.c / pyzk:
/// چک‌سام، ساخت بسته TCP، کد/کدگشایی ساعت و کلید ارتباطی (CommKey).
/// </summary>
public static class ZkCodec
{
    // ---------- checksum ----------
    //
    // الگوریتم مرجع (zkemsdk.c — به‌صورت بهینه‌شده):
    //   جمع کلمات 16بیتی Little-Endian با مدول 65535 (نه 65536!):
    //   هر بار که جمع از 65535 بیشتر شد «65535» کم می‌شود؛ در پایان مکمل یک (بدون صفر شدن) گرفته می‌شود
    //   و با مدول 65535 به بازه [0..65534] برمی‌گردد. این رفتار خاص دستگاه‌های ZK است.

    /// <summary>محاسبه چک‌سام روی بایت‌های ورودی (فرمان + داده) — مطابق __create_checksum در pyzk.</summary>
    public static ushort Checksum(ReadOnlySpan<byte> p)
    {
        uint checksum = 0;
        int i = 0;
        while (i + 1 < p.Length)
        {
            checksum += p[i] | ((uint)p[i + 1] << 8);
            if (checksum > ZkConsts.UshrtMax) checksum -= ZkConsts.UshrtMax;
            i += 2;
        }
        if (i < p.Length) checksum += p[i]; // بایت فرد آخر

        while (checksum > ZkConsts.UshrtMax) checksum -= ZkConsts.UshrtMax;

        // مکمل یک با مدول 65535 (رفتار پایتون: ~x سپس افزودن 65535 تا مثبت شود)
        long r = ~(long)checksum;
        while (r < 0) r += ZkConsts.UshrtMax;
        return (ushort)r;
    }

    // ---------- بسته‌بندی ----------

    /// <summary>
    /// بسته داخلی (همان قالب UDP): cmd(2) chk(2) sid(2) rid(2) data
    /// مطابق __create_header در pyzk: چک‌سام روی نسخه با chk=0 محاسبه و سپس جای‌گذاری می‌شود؛
    /// replyId در بسته نهایی یکی جلو می‌رود (wrap با مدول 65535).
    /// </summary>
    public static byte[] BuildPacket(ushort cmd, ReadOnlySpan<byte> data, ushort sessionId,
                                     ushort replyId, out ushort nextReplyId)
    {
        Span<byte> buf = stackalloc byte[8 + data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, cmd);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.Slice(2), 0); // جای‌نگهدار چک‌سام
        BinaryPrimitives.WriteUInt16LittleEndian(buf.Slice(4), sessionId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.Slice(6), replyId);
        data.CopyTo(buf.Slice(8));

        ushort chk = Checksum(buf);

        nextReplyId = (ushort)(replyId + 1);
        if (nextReplyId >= ZkConsts.UshrtMax) nextReplyId -= ZkConsts.UshrtMax;

        byte[] pkt = new byte[8 + data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(pkt, cmd);
        BinaryPrimitives.WriteUInt16LittleEndian(pkt.AsSpan(2), chk);
        BinaryPrimitives.WriteUInt16LittleEndian(pkt.AsSpan(4), sessionId);
        BinaryPrimitives.WriteUInt16LittleEndian(pkt.AsSpan(6), nextReplyId);
        data.CopyTo(pkt.AsSpan(8));
        return pkt;
    }

    /// <summary>کاور TCP: سرصفحه 8 بایتی (50 50 82 7d + طول LE) + بسته داخلی.</summary>
    public static byte[] Frame(byte[] packet)
    {
        byte[] top = new byte[8 + packet.Length];
        top[0] = 0x50; top[1] = 0x50; top[2] = 0x82; top[3] = 0x7d;
        BinaryPrimitives.WriteUInt32LittleEndian(top.AsSpan(4), (uint)packet.Length);
        packet.CopyTo(top, 8);
        return top;
    }

    /// <summary>تشخیص کاور TCP از روی سرصفحه 8 بایتی و استخراج طول بسته داخلی؛ در غیر این صورت صفر.</summary>
    public static uint TcpTopLength(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 8) return 0;
        if (frame[0] == 0x50 && frame[1] == 0x50 && frame[2] == 0x82 && frame[3] == 0x7d)
            return BinaryPrimitives.ReadUInt32LittleEndian(frame.Slice(4));
        return 0;
    }

    // ---------- ساعت دستگاه ----------

    /// <summary>کدگذاری زمان برای CMD_SET_TIME — فرمول zkemsdk.c EncodeTime.</summary>
    public static uint EncodeTime(DateTime t) =>
        (uint)((((t.Year % 100) * 12 * 31) + ((t.Month - 1) * 31) + t.Day - 1) * 86400L
               + ((t.Hour * 60 + t.Minute) * 60 + t.Second));

    /// <summary>کدگشایی 4 بایت زمان دستگاه — فرمول zkemsdk.c DecodeTime.</summary>
    public static DateTime DecodeTime(ReadOnlySpan<byte> b4)
    {
        uint t = BinaryPrimitives.ReadUInt32LittleEndian(b4);
        uint sec = t % 60; t /= 60;
        uint min = t % 60; t /= 60;
        uint hour = t % 24; t /= 24;
        uint day = t % 31 + 1; t /= 31;
        uint month = t % 12 + 1; t /= 12;
        uint year = t + 2000;
        return new DateTime((int)year, (int)month, (int)day, (int)hour, (int)min, (int)sec, DateTimeKind.Local);
    }

    // ---------- کلید ارتباطی ----------

    /// <summary>
    /// درهم‌ریزی کلید ارتباطی برای CMD_AUTH — پورت مستقیم make_commkey از commpro.c (MakeKey).
    /// </summary>
    public static byte[] MakeCommKey(uint key, uint sessionId, byte ticks = 50)
    {
        // وارونه‌سازی بیتی 32 بیتی کلید
        uint k = 0;
        for (int i = 0; i < 32; i++)
        {
            k <<= 1;
            if ((key & (1u << i)) != 0) k |= 1u;
        }
        k += sessionId; // جمع مدولار 32 بیتی

        byte[] b = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, k);

        // XOR با "ZKSO"
        b[0] ^= (byte)'Z';
        b[1] ^= (byte)'K';
        b[2] ^= (byte)'S';
        b[3] ^= (byte)'O';

        // جابه‌جایی دو کلمه 16 بیتی: [b2,b3,b0,b1]
        byte b0 = b[0], b1 = b[1];
        b[0] = b[2]; b[1] = b[3]; b[2] = b0; b[3] = b1;

        // گام نهایی با ticks: r0^=B, r1^=B, r2=B, r3^=B
        b[0] ^= ticks;
        b[1] ^= ticks;
        b[2] = ticks;
        b[3] ^= ticks;
        return b;
    }

    // ---------- کمکی ----------

    /// <summary>خواندن رشته نال-پایان از بازه ثابت (فیلدهای ساختار کاربر).</summary>
    public static string NullTerminated(byte[] buf, int offset, int length, int enc = 1256)
    {
        int end = Math.Min(offset + length, buf.Length);
        int len = 0;
        while (offset + len < end && buf[offset + len] != 0) len++;
        return DecodeString(buf, offset, len);
    }

    /// <summary>کدگشایی رشته با fallback: UTF-8 → ویندوز-1256 (فارسی) → ASCII.</summary>
    public static string DecodeString(byte[] buf, int offset, int length)
    {
        if (length <= 0) return "";
        // 1) UTF-8
        try
        {
            var s = System.Text.Encoding.UTF8.GetString(buf, offset, length);
            if (!s.Contains('\uFFFD')) return s.TrimEnd('\r', '\n', ' ');
        }
        catch { /* fallback */ }
        // 2) ویندوز-1256 (عربی/فارسی) — روی لینوکس در دسترس نیست؛ ASCII امن را امتحان کن
        try
        {
            var enc = System.Text.Encoding.GetEncoding(1256);
            return enc.GetString(buf, offset, length).TrimEnd('\r', '\n', ' ');
        }
        catch
        {
            var sb = new System.Text.StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                byte c = buf[offset + i];
                sb.Append(c < 128 ? (char)c : '?');
            }
            return sb.ToString();
        }
    }

    public static string Hex(byte[] bytes, int max = 48)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Math.Min(bytes.Length, max); i++)
            sb.Append(bytes[i].ToString("x2"));
        if (bytes.Length > max) sb.Append("…");
        return sb.ToString();
    }
}

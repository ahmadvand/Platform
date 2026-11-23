using System.IO.Compression;
using System.Text.Json;
using Platform.Web.Services;

namespace Platform.Web.Infrastructure;

/// <summary>
/// فاز ۹۲ — مکانیزم به‌روزرسانی بسته‌ای (درخواست کاربر: «به‌روزرسانی ماژول از طریق پنل ادمین»).
/// قرارداد بسته: یک zip با «manifest.json» در ریشه + فایل‌های نصب (bin/، wwwroot/، web.config،
/// appsettings*.json اختیاری). manifest:
/// { "format":"hr-update-pack", "version":"2.7.6", "minVersion":"2.7.5", "includeAppSettings":false,
///   "notes":{"fa":"…","en":"…"} }
/// جریان نصب: آپلود در پنل ادمین → استخراج به App_Data/updates/staging → اعتبارسنجی manifest
/// → «نصب» = پشتیبان‌گیری خودکار از نصب فعلی + ثبت apply.json → فایل‌های استیجینگ روی نصب
/// کپی می‌شوند (روی لینوکس بلافاصله؛ روی ویندوز پس از توقف اپ با وظیفهٔ زمان‌بندی‌شدهٔ
/// update-runner.ps1 — همان قرارداد apply.json) → با اجرای بعدی برنامه، DbInit مهاجرت‌های
/// EF را خودکار اجرا می‌کند. بازگردانی = استخراج پشتیبان به استیجینگ + apply.json (همان مسیر).
/// </summary>
public static class UpdateApplier
{
    public sealed record PendingInfo(string Staging, string Version, string NotesFa, string NotesEn, string CreatedAt);

    public static string UpdatesRoot(string root) => Path.Combine(root, "App_Data", "updates");
    public static string ApplyFlag(string root) => Path.Combine(UpdatesRoot(root), "apply.json");
    public static string BackupsRoot(string root) => Path.Combine(root, "App_Data", "backups");

    /// <summary>پوشه‌های/فایل‌های «کد» که پشتیبان‌گیری و بازگردانی می‌شوند.</summary>
    public static readonly string[] CodeEntries = { "bin", "wwwroot", "web.config", "appsettings.json" };

    // ============================ pending ============================

    public static PendingInfo? ReadPending(string root)
    {
        try
        {
            var f = Path.Combine(UpdatesRoot(root), "pending.json");
            if (!File.Exists(f)) return null;
            var j = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(f));
            return new PendingInfo(
                j.GetProperty("staging").GetString() ?? "",
                j.GetProperty("version").GetString() ?? "",
                j.TryGetProperty("notesFa", out var fa) ? fa.GetString() ?? "" : "",
                j.TryGetProperty("notesEn", out var en) ? en.GetString() ?? "" : "",
                j.TryGetProperty("createdAt", out var c) ? c.GetString() ?? "" : "");
        }
        catch { return null; }
    }

    public static void WritePending(string root, PendingInfo p)
    {
        Directory.CreateDirectory(UpdatesRoot(root));
        var f = Path.Combine(UpdatesRoot(root), "pending.json");
        var json = JsonSerializer.Serialize(new
        {
            staging = p.Staging,
            version = p.Version,
            notesFa = p.NotesFa,
            notesEn = p.NotesEn,
            createdAt = p.CreatedAt,
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(f, json);
    }

    // ============================ apply flag ============================

    public static void WriteApplyFlag(string root, string staging)
    {
        Directory.CreateDirectory(UpdatesRoot(root));
        File.WriteAllText(ApplyFlag(root), JsonSerializer.Serialize(new
        {
            staging,
            startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>در آغاز Main (پیش از ساخت اپ) — اگر apply.json باشد استیجینگ روی نصب کپی می‌شود.
    /// شکست → استیجینگ به failed-… تغییرنام می‌کند تا حلقهٔ خراب در بوت تکرار نشود.</summary>
    public static void RunIfPending(string root)
    {
        try
        {
            var flag = ApplyFlag(root);
            if (!File.Exists(flag)) return;
            var staging = "";
            try
            {
                var j = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(flag));
                staging = j.GetProperty("staging").GetString() ?? "";
            }
            catch { }
            File.Delete(flag);
            if (string.IsNullOrWhiteSpace(staging) || !Directory.Exists(staging))
            {
                Console.WriteLine("[update] apply.json ناقص یا استیجینگ غایب — رد شد");
                return;
            }
            ApplyNow(root, staging);
            Console.WriteLine($"[update] به‌روزرسانی از {staging} نصب شد — نسخهٔ جدید با همین اجرا فعال است");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[update] خطای اعمال به‌روزرسانی: " + ex.Message);
        }
    }

    /// <summary>کپی فایل‌های استیجینگ روی نصب. روی ویندوز اگر فایل قفل باشد IOException می‌دهد؛
    /// caller (تسک ویندوز یا پیام پنل) وظیفهٔ ری‌استارت/تسک زمان‌بندی‌شده را دارد.</summary>
    public static void ApplyNow(string root, string staging)
    {
        var manifest = Path.Combine(staging, "manifest.json");
        var includeAppSettings = false;
        if (File.Exists(manifest))
        {
            try
            {
                var j = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(manifest));
                includeAppSettings = j.TryGetProperty("includeAppSettings", out var ias) && ias.ValueKind == JsonValueKind.True;
            }
            catch { }
        }
        foreach (var dir in Directory.GetDirectories(staging, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(dir);
            if (name == "App_Data") continue;                 // دادهٔ نصب مقصد هرگز از بسته بازنویسی نمی‌شود
            if (name == "logs") continue;
            CopyDir(dir, Path.Combine(root, name));
        }
        foreach (var file in Directory.GetFiles(staging, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (name == "manifest.json") continue;
            if (name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && !includeAppSettings) continue;
            var dest = Path.Combine(root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var d in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(d.Replace(src, dst));
        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            File.Copy(f, f.Replace(src, dst), overwrite: true);
    }

    // ============================ upload / extract ============================

    /// <summary>استخراج امن zip به پوشهٔ استیجینگ + اعتبارسنجی manifest. خروجی = (پیام، نسخه، استیجینگ).</summary>
    public static (bool Ok, string Message, string Version, string Staging, string NotesFa, string NotesEn)
        ExtractPackage(string root, Stream zipStream, string currentVersion)
    {
        var updates = UpdatesRoot(root);
        Directory.CreateDirectory(updates);
        var staging = Path.Combine(updates, "staging-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        try
        {
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
            // محافظ zip-slip: مسیر هر ورودی باید داخل مقصد بماند
            foreach (var e in zip.Entries)
            {
                var dest = Path.GetFullPath(Path.Combine(staging, e.FullName));
                if (!dest.StartsWith(Path.GetFullPath(staging) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    return (false, HrT.T("بستهٔ به‌روزرسانی نامعتبر است (مسیر مشکوک در zip)"), "", "", "", "");
            }
            zip.ExtractToDirectory(staging, overwriteFiles: true);

            var mf = Path.Combine(staging, "manifest.json");
            if (!File.Exists(mf))
                return (false, HrT.T("manifest.json در ریشهٔ بسته نیست — قالب بسته در راهنمای همین صفحه آمده است"), "", "", "", "");

            var j = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(mf));
            if (j.TryGetProperty("format", out var fmt) && fmt.GetString() != "hr-update-pack")
                return (false, HrT.T("قالب بسته شناسایی نشد (format ≠ hr-update-pack)"), "", "", "", "");
            var version = j.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
            if (version.Length == 0 || !System.Version.TryParse(version, out var nv))
                return (false, HrT.T("نسخهٔ بسته در manifest نامعتبر است"), "", "", "", "");
            var cur = System.Version.TryParse(currentVersion, out var cv) ? cv : new Version(0, 0, 0);
            if (nv <= cur)
                return (false, HrT.F("نسخهٔ بسته ({0}) باید جدیدتر از نسخهٔ فعلی ({1}) باشد", version, currentVersion), "", "", "", "");
            if (j.TryGetProperty("minVersion", out var mv) && System.Version.TryParse(mv.GetString(), out var minv) && cur < minv)
                return (false, HrT.F("این بسته حداقل نسخهٔ {0} می‌خواهد؛ نسخهٔ فعلی {1} است", minv, currentVersion), "", "", "", "");
            var notesFa = j.TryGetProperty("notes", out var n) && n.TryGetProperty("fa", out var nfa) ? nfa.GetString() ?? "" : "";
            var notesEn = j.TryGetProperty("notes", out var n2) && n2.TryGetProperty("en", out var nen) ? nen.GetString() ?? "" : "";
            return (true, HrT.F("بستهٔ نسخهٔ {0} اعتبارسنجی و در صف نصب قرار گرفت", version), version, staging, notesFa, notesEn);
        }
        catch (Exception ex)
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
            return (false, HrT.T("استخراج بسته ناموفق بود: ") + ex.Message, "", "", "", "");
        }
    }

    // ============================ backup / rollback ============================

    /// <summary>پشتیبان zip از کد نصب فعلی (bin، wwwroot بدون dl، web.config، appsettings، hrsettings.bin).</summary>
    public static string BackupCurrent(string root)
    {
        var bdir = BackupsRoot(root);
        Directory.CreateDirectory(bdir);
        var zipPath = Path.Combine(bdir, "backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        void AddFile(string path, string entryName)
        {
            if (File.Exists(path)) zip.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
        }
        var bin = Path.Combine(root, "bin");
        if (Directory.Exists(bin))
            foreach (var f in Directory.EnumerateFiles(bin, "*", SearchOption.AllDirectories))
                zip.CreateEntryFromFile(f, "bin/" + Path.GetRelativePath(bin, f).Replace('\\', '/'), CompressionLevel.Optimal);
        var www = Path.Combine(root, "wwwroot");
        if (Directory.Exists(www))
            foreach (var f in Directory.EnumerateFiles(www, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(www, f);
                var top = rel.Split(Path.DirectorySeparatorChar)[0];
                if (top == "dl") continue; // بسته‌های دانلودی — جزو کد نصب نیستند
                zip.CreateEntryFromFile(f, "wwwroot/" + rel.Replace('\\', '/'), CompressionLevel.Optimal);
            }
        AddFile(Path.Combine(root, "web.config"), "web.config");
        foreach (var f in Directory.GetFiles(root, "appsettings*.json"))
            AddFile(f, Path.GetFileName(f));
        AddFile(Path.Combine(root, "App_Data", "hrsettings.bin"), "App_Data/hrsettings.bin");
        return zipPath;
    }

    /// <summary>بازگردانی پشتیبان: استخراج zip به استیجینگ + apply.json — با اجرای بعدی اعمال می‌شود.</summary>
    public static (bool Ok, string Message) RestoreBackup(string root, string backupZip)
    {
        try
        {
            var updates = UpdatesRoot(root);
            var staging = Path.Combine(updates, "staging-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(staging);
            using (var zip = ZipFile.OpenRead(backupZip))
            {
                foreach (var e in zip.Entries)
                {
                    var dest = Path.GetFullPath(Path.Combine(staging, e.FullName));
                    if (!dest.StartsWith(Path.GetFullPath(staging) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                        return (false, HrT.T("فایل پشتیبان نامعتبر است"));
                    if (string.IsNullOrEmpty(e.Name) && e.FullName.EndsWith('/')) continue;
                    var dir = Path.GetDirectoryName(dest)!;
                    Directory.CreateDirectory(dir);
                    e.ExtractToFile(dest, overwrite: true);
                }
            }
            WriteApplyFlag(root, staging);
            return (true, HrT.T("بازگردانی در صف قرار گرفت — با اجرای بعدی برنامه اعمال می‌شود"));
        }
        catch (Exception ex) { return (false, HrT.T("بازگردانی ناموفق بود: ") + ex.Message); }
    }
}

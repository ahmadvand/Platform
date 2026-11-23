using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Platform.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۹۲ — «به‌روزرسانی سامانه» (درخواست کاربر): آپلود بستهٔ به‌روزرسانی ماژول‌ها از پنل ادمین،
/// پشتیبان‌گیری خودکار، نصب با اجرای بعدی (یا فوری روی لینوکس)، بازگردانی پشتیبان، و راهنمای کامل
/// ساخت بسته. فاز ۹۳ — پرمیشن اختصاصی updates.manage (والد settings.manage) برای مشاهده و تغییرها.
/// </summary>
[HasPerm("updates.manage")]
public class UpdatesController(AppDbContext db, HrBaseDbContext baseDb, IWebHostEnvironment env, ICurrentUser me)
    : MvcBase(db, me)
{
    private readonly HrBaseDbContext Base = baseDb;
    private readonly IWebHostEnvironment _env = env;

    private string Root => _env.ContentRootPath;

    public async Task<IActionResult> Index()
    {
        ViewBag.Version = BuildInfo.Version;
        ViewBag.Vendor = BuildInfo.Vendor;
        ViewBag.Pending = UpdateApplier.ReadPending(Root);
        ViewBag.AlreadyFlagged = System.IO.File.Exists(UpdateApplier.ApplyFlag(Root));

        var bdir = UpdateApplier.BackupsRoot(Root);
        var backups = new List<(string Name, long Bytes, DateTime At)>();
        if (Directory.Exists(bdir))
            foreach (var f in Directory.GetFiles(bdir, "backup-*.zip"))
                backups.Add((Path.GetFileName(f), new FileInfo(f).Length, System.IO.File.GetLastWriteTime(f)));
        backups = backups.OrderByDescending(b => b.At).ToList();
        ViewBag.Backups = backups;

        // فاز ۱۳۵ (مورد ۲۰) — پشتیبان‌های دیتابیس + تنظیمات زمان‌بندی
        var dbdir = DbBackup.BackupsDbRoot(Root);
        var dbBackups = new List<(string Name, long Bytes, DateTime At)>();
        if (Directory.Exists(dbdir))
            foreach (var f in Directory.GetFiles(dbdir, "db-*.zip"))
                dbBackups.Add((Path.GetFileName(f), new FileInfo(f).Length, System.IO.File.GetLastWriteTime(f)));
        dbBackups = dbBackups.OrderByDescending(b => b.At).ToList();
        ViewBag.DbBackups = dbBackups;
        ViewBag.S = await Db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == "default") ?? new AppSettings();

        // فهرست ماژول‌های نصب‌شده (برای راهنمای «به‌روزرسانی یک ماژول»)
        ViewBag.Modules = Platform.Contracts.HrModuleRuntime.UiModules()
            .Select(m => (m.Code, m.Title)).OrderBy(m => m.Code).ToList();

        await Audit("view_updates", "Updates", null, null);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("updates.manage")]
    [RequestSizeLimit(300_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 300_000_000)]
    public async Task<IActionResult> Upload(IFormFile? package)
    {
        if (package is null || package.Length == 0)
        {
            ToastErr(HrT.T("فایل بستهٔ به‌روزرسانی انتخاب نشده است"));
            return RedirectToAction(nameof(Index));
        }
        if (!package.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ToastErr(HrT.T("فقط فایل zip پذیرفته می‌شود"));
            return RedirectToAction(nameof(Index));
        }
        // بستهٔ در صف — اول باید نصب یا لغو شود
        if (UpdateApplier.ReadPending(Root) is not null)
        {
            ToastErr(HrT.T("یک بستهٔ به‌روزرسانی در صف نصب است — اول آن را نصب یا لغو کنید"));
            return RedirectToAction(nameof(Index));
        }
        await using var stream = package.OpenReadStream();
        var (ok, msg, version, staging, notesFa, notesEn) =
            UpdateApplier.ExtractPackage(Root, stream, BuildInfo.Version);
        await Audit(ok ? "upload_update_package" : "upload_update_package_failed", "Updates", null, new { package.FileName, size = package.Length, msg });
        if (!ok) { ToastErr(msg); return RedirectToAction(nameof(Index)); }
        UpdateApplier.WritePending(Root, new UpdateApplier.PendingInfo(staging, version, notesFa, notesEn, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        ToastOk(msg);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>نصب بستهٔ در صف: پشتیبان خودکار + ثبت apply.json + تلاش برای نصب فوری.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("updates.manage")]
    public async Task<IActionResult> Apply()
    {
        var pending = UpdateApplier.ReadPending(Root);
        if (pending is null || !Directory.Exists(pending.Staging))
        {
            ToastErr(HrT.T("بسته‌ای در صف نصب نیست"));
            return RedirectToAction(nameof(Index));
        }
        var backupPath = UpdateApplier.BackupCurrent(Root);
        UpdateApplier.WriteApplyFlag(Root, pending.Staging);
        var appliedNow = false;
        try
        {
            UpdateApplier.ApplyNow(Root, pending.Staging);   // لینوکس: فایل قفل نمی‌شود — بلافاصله نصب
            System.IO.File.Delete(UpdateApplier.ApplyFlag(Root));
            appliedNow = true;
        }
        catch (IOException)
        {
            // ویندوز: DLLهای در حال اجرا قفل‌اند — update-runner.ps1 (وظیفهٔ زمان‌بندی‌شدهٔ ویندوز)
            // یا اجرای بعدی برنامه apply.json را اعمال می‌کند.
        }
        try { Directory.Delete(pending.Staging, recursive: true); } catch { }
        try { System.IO.File.Delete(Path.Combine(UpdateApplier.UpdatesRoot(Root), "pending.json")); } catch { }
        await Audit("apply_update", "Updates", null, new { pending.Version, backup = Path.GetFileName(backupPath), appliedNow });
        ToastOk(appliedNow
            ? HrT.T("به‌روزرسانی نصب شد — با ری‌استارت برنامه، نسخهٔ جدید قطعاً فعال می‌شود")
            : HrT.T("بستهٔ به‌روزرسانی آمادهٔ نصب شد — پس از توقف/ری‌استارت سرویس، وظیفهٔ به‌روزرسان ویندوز یا اجرای بعدی برنامه آن را نصب می‌کند"));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>لغو بستهٔ در صف.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("updates.manage")]
    public async Task<IActionResult> Discard()
    {
        var pending = UpdateApplier.ReadPending(Root);
        if (pending is not null)
        {
            try { Directory.Delete(pending.Staging, recursive: true); } catch { }
            try { System.IO.File.Delete(Path.Combine(UpdateApplier.UpdatesRoot(Root), "pending.json")); } catch { }
            await Audit("discard_update_package", "Updates", null, new { pending.Version });
        }
        ToastOk(HrT.T("بستهٔ در صف لغو شد"));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>پشتیبان‌گیری دستی از نصب فعلی.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("updates.manage")]
    public async Task<IActionResult> BackupNow()
    {
        var zip = UpdateApplier.BackupCurrent(Root);
        await Audit("backup_install", "Updates", null, new { zip = Path.GetFileName(zip) });
        ToastOk(HrT.T("پشتیبان نصب ساخته شد: ") + Path.GetFileName(zip));
        return RedirectToAction(nameof(Index));
    }

    // ======================================================================
    // فاز ۱۳۵ (مورد ۲۰ ROADMAP-ADDITIONS) — پشتیبان‌گیری زمان‌بندی‌شدهٔ دیتابیس
    // تنظیم ساعت/فعال‌سازی + اجرای فوری؛ نگهداشت N نسخه در خودِ سرویس اعمال می‌شود.
    // ======================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("updates.manage")]
    public async Task<IActionResult> SaveDbBackup(bool backupEnabled, string backupTime, int backupRetention)
    {
        if (!TimeSpan.TryParse(backupTime, out _))
        {
            ToastErr(HrT.T("ساعت نامعتبر است — قالب HH:mm"));
            return RedirectToAction(nameof(Index));
        }
        var s = await Db.AppSettings.FirstOrDefaultAsync(x => x.Id == "default");
        if (s is null)
        {
            s = new AppSettings { Id = "default" };
            Db.AppSettings.Add(s);
        }
        s.BackupEnabled = backupEnabled;
        s.BackupTime = backupTime;
        s.BackupRetention = Math.Clamp(backupRetention, 1, 90);
        await Db.SaveChangesAsync();
        await Audit("update", "db-backup-settings", null, new { backupEnabled, backupTime, s.BackupRetention });
        ToastOk(HrT.T("تنظیمات پشتیبان‌گیری خودکار ذخیره شد"));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("updates.manage")]
    public async Task<IActionResult> DbBackupNow()
    {
        var s = await Db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == "default");
        var (ok, name, removed) = DbBackup.CreateBackup(Root, s?.BackupRetention ?? 7, Db);
        await Audit(ok ? "db_backup" : "db_backup_failed", "Updates", null, new { name, removed });
        if (ok) ToastOk(HrT.T("پشتیبان دیتابیس ساخته شد: ") + name + HrT.F(" ({0} نسخهٔ قدیمی حذف شد)", removed));
        else ToastErr(HrT.T("پشتیبان‌گیری ناموفق: ") + name);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("updates.manage")]
    public IActionResult DeleteDbBackup(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\') || name.Contains(".."))
        {
            ToastErr(HrT.T("نام پشتیبان نامعتبر است"));
            return RedirectToAction(nameof(Index));
        }
        var path = Path.Combine(DbBackup.BackupsDbRoot(Root), name);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        ToastOk(HrT.T("پشتیبان دیتابیس حذف شد"));
        return RedirectToAction(nameof(Index));
    }


    /// <summary>بازگردانی پشتیبان کد — با اجرای بعدی برنامه اعمال می‌شود.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("updates.manage")]
    public async Task<IActionResult> Restore(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\') || name.Contains(".."))
        {
            ToastErr(HrT.T("نام پشتیبان نامعتبر است"));
            return RedirectToAction(nameof(Index));
        }
        var path = Path.Combine(UpdateApplier.BackupsRoot(Root), name);
        if (!System.IO.File.Exists(path))
        {
            ToastErr(HrT.T("فایل پشتیبان یافت نشد"));
            return RedirectToAction(nameof(Index));
        }
        var (ok, msg) = UpdateApplier.RestoreBackup(Root, path);
        await Audit(ok ? "restore_backup" : "restore_backup_failed", "Updates", null, new { name, msg });
        if (ok) ToastOk(msg); else ToastErr(msg);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPerm("updates.manage")]
    public async Task<IActionResult> DeleteBackup(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\') || name.Contains(".."))
        {
            ToastErr(HrT.T("نام پشتیبان نامعتبر است"));
            return RedirectToAction(nameof(Index));
        }
        var path = Path.Combine(UpdateApplier.BackupsRoot(Root), name);
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
            await Audit("delete_backup", "Updates", null, new { name });
        }
        ToastOk(HrT.T("پشتیبان حذف شد"));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>دانلود پشتیبان (برای نگهداری بیرون از سرور).</summary>
    [HttpGet]
    [HasPerm("updates.manage")]
    public IActionResult DownloadBackup(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\') || name.Contains(".."))
            return NotFound();
        var path = Path.Combine(UpdateApplier.BackupsRoot(Root), name);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/zip", name);
    }
}

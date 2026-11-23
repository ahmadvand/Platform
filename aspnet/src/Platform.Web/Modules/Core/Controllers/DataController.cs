using Platform.Web.Data;
using Platform.Web.Domain;
using Platform.Web.Infrastructure;
using Platform.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Platform.Web.Controllers;

/// <summary>
/// فاز ۱۱۲ — هاب «درون‌ریزی و برون‌بری داده» (درخواست کاربر).
/// کاتالوگ از مدل EF کانتکست مرکب ساخته می‌شود — همهٔ خانواده‌های دادهٔ
/// همهٔ ماژول‌های نصب‌شده بدون کدِ اختصاصی. برون‌بری: xlsx/csv/json؛
/// درون‌ریزی: قالب خالی + پیش‌نمایش با گزارش + ثبت تأییدشده.
/// مشاهده/عملیات: data.exchange (فقط مدیران مجاز).
/// </summary>
[HasPerm("data.exchange")]
public class DataController(AppDbContext db, ICurrentUser me,
    Platform.Web.Modules.ModuleStateService modsvc) : MvcBase(db, me)
{
    // فاز ۱۲۲ — ملاک فعال‌بودن ماژول مالک (نصب/دیتا/مجوز/خاموش‌نشده) — همان ملاک منو
    private Platform.Web.Services.DataExchange.DataEntity? Ent(string? entity) =>
        Platform.Web.Services.DataExchange.Find(Db, entity, code => modsvc.Enabled(code));

    public IActionResult Index()
    {
        ViewBag.Catalog = Platform.Web.Services.DataExchange.Catalog(Db, code => modsvc.Enabled(code));
        return View();
    }

    // ---------- برون‌بری ----------

    [HttpGet]
    public IActionResult Export(string? entity, string? fmt)
    {
        var ent = Ent(entity);
        if (ent is null) return NotFound();
        fmt = (fmt ?? "xlsx").Trim().ToLowerInvariant();
        var (bytes, mime, name) = fmt switch
        {
            "csv" => (DataExchange.ExportCsv(Db, ent), "text/csv; charset=utf-8", $"{ent.ClrName}.csv"),
            "json" => (DataExchange.ExportJson(Db, ent), "application/json", $"{ent.ClrName}.json"),
            _ => (DataExchange.ExportXlsx(Db, ent), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{ent.ClrName}.xlsx"),
        };
        _ = Audit("export", ent.ClrName, null, new { fmt, count = ent.Count });
        return File(bytes, mime, name);
    }

    [HttpGet]
    public IActionResult Template(string? entity)
    {
        var ent = Ent(entity);
        if (ent is null) return NotFound();
        return File(DataExchange.ImportTemplate(ent),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"قالب-{ent.ClrName}.xlsx");
    }

    // ---------- درون‌ریزی (پیش‌نمایش ← تأیید ← ثبت) ----------

    [HttpGet]
    public IActionResult ImportForm(string? entity)
    {
        var ent = Ent(entity);
        if (ent is null) return NotFound();
        if (!ent.CanImport)
        {
            ToastErr(HrT.F("خانوادهٔ «{0}» درون‌ریزی ندارد (فقط برون‌بری) — دادهٔ لاگ/محاسباتی از مسیر رسمی خودش ساخته می‌شود", ent.Title));
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Entity = ent;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> ImportPreview(string? entity, IFormFile? file)
    {
        var ent = Ent(entity);
        if (ent is null) return NotFound();
        if (!ent.CanImport)
        {
            ToastErr(HrT.F("خانوادهٔ «{0}» درون‌ریزی ندارد", ent.Title));
            return RedirectToAction(nameof(Index));
        }
        if (file is null || file.Length == 0)
        {
            ToastErr(HrT.T("فایلی انتخاب نشده است"));
            return RedirectToAction(nameof(ImportForm), new { entity = ent.Key });
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;
        var parsed = DataExchange.ParseFile(ent, ms, file.FileName);
        parsed.SourceFile = file.FileName;

        ViewBag.Entity = ent;
        ViewBag.Parsed = parsed;
        // ردیف‌ها با فیلد مخفی به مرحلهٔ تأیید می‌روند (همان الگوی ورود گروهی پرسنل)
        ViewBag.RowsJson = System.Text.Json.JsonSerializer.Serialize(parsed.Rows);
        return View(nameof(ImportPreview));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportCommit(string? entity, string? rowsJson)
    {
        var ent = Ent(entity);
        if (ent is null) return NotFound();
        if (!ent.CanImport)
        {
            ToastErr(HrT.F("خانوادهٔ «{0}» درون‌ریزی ندارد", ent.Title));
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(rowsJson))
        {
            ToastErr(HrT.T("داده‌ای برای ثبت نیست"));
            return RedirectToAction(nameof(ImportForm), new { entity = ent.Key });
        }
        List<Dictionary<string, string?>> rows;
        try
        {
            rows = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string?>>>(rowsJson) ?? new();
        }
        catch
        {
            ToastErr(HrT.T("دادهٔ ارسالی نامعتبر است"));
            return RedirectToAction(nameof(ImportForm), new { entity = ent.Key });
        }
        if (rows.Count == 0 || rows.Count > DataExchange.MaxImportRows)
        {
            ToastErr(HrT.F("تعداد ردیف نامعتبر است (مجاز: ۱ تا {0})", HrT.Num(DataExchange.MaxImportRows)));
            return RedirectToAction(nameof(ImportForm), new { entity = ent.Key });
        }

        // PasswordHash فقط برای خانوادهٔ User و فقط از JSON (هاش bcrypt) — مهاجرت رفت‌وبرگشتی
        var allowHash = ent.ClrName == "User" && rowsJson.Contains("\"PasswordHash\"");
        var report = DataExchange.Commit(Db, ent, rows, allowHash);
        await Audit("import", ent.ClrName, null,
            new { rows = rows.Count, report.Inserted, report.Updated, report.Skipped, errors = report.Errors.Count });
        if (report.Errors.Count == 0)
        {
            ToastOk(HrT.F("درون‌ریزی «{0}» انجام شد — {1} جدید، {2} به‌روزرسانی", ent.Title, HrT.Num(report.Inserted), HrT.Num(report.Updated))
                + (report.Skipped > 0 ? " · " + HrT.F("{0} ردیف نادیده گرفته شد", HrT.Num(report.Skipped)) : ""));
        }
        else
        {
            ToastErr(HrT.F("درون‌ریزی «{0}» با خطا همراه بود — {1} جدید، {2} به‌روزرسانی، {3} خطا (گزارش در پایین صفحه)",
                ent.Title, HrT.Num(report.Inserted), HrT.Num(report.Updated), HrT.Num(report.Errors.Count)));
        }
        ViewBag.Entity = ent;
        ViewBag.Report = report;
        return View("ImportResult");
    }
}

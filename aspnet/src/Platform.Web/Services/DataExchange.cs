using ClosedXML.Excel;
using Platform.Web.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Platform.Web.Services;

// ============================================================
// فاز ۱۱۲ — هاب «درون‌ریزی و برون‌بری داده» (درخواست کاربر:
// «امکان Import/Export برای هر داده‌ای که امکانپذیره» — مهاجرت از
// نرم‌افزارهای دیگر و خروجی برای نرم‌افزارهای دیگر).
// فاز ۱۲۲ — کاتالوگ «اعلامی‌محور» شد (درخواست کاربر: «هر ماژول مواردی را که
// باید درون‌ریزی/برون‌بری داشته باشد به هسته اعلام کند؛ ماژول غایب = هیچ»):
// هر ماژول دیتا با IHrDataModule.DataExchange خانواده‌های خودش را اعلام می‌کند
// (عنوان/گروه/مجوز درون‌ریزی/کلید تجاری) و هستهٔ پلتفرم اعلامش در CoreModule است.
// هاست هیچ فهرست ایستایی از خانواده‌ها ندارد؛ خانوادهٔ اعلام‌نشده یا متعلق به
// ماژول غایب/خاموش در هاب رندر نمی‌شود.
// قالب‌ها: XLSX (اکسل فارسی‌دوست) + CSV (UTF-8 BOM) + JSON (رفت‌وبرگشت کامل).
// امنیت: ستون‌های رمز/توکن هرگز برون‌بری نمی‌شوند؛ ردیف‌های
// RefreshToken/PasswordHistory/PasswordReset اصلاً در کاتالوگ نیستند؛
// درون‌ریزی = پیش‌نمایش + گزارش خطا + ثبت با تأیید (الگوی ورود گروهی پرسنل).
// ============================================================
public static class DataExchange
{
    /// <summary>حداکثر ردیف برون‌بری در هر درخواست (محافظ حافظه).</summary>
    public const int MaxExportRows = 200_000;
    /// <summary>حداکثر ردیف درون‌ریزی در هر فایل.</summary>
    public const int MaxImportRows = 20_000;
    /// <summary>حداکثر ردیف پیش‌نمایش در صفحهٔ تأیید.</summary>
    public const int PreviewRows = 60;

    /// <summary>ستون‌های حساس — هرگز در هیچ قالبی برون‌بری/درون‌ریزی نمی‌شوند.</summary>
    private static readonly HashSet<string> SecretColumns = new(StringComparer.Ordinal)
    { "PasswordHash", "TokenHash", "WebhookToken", "TemplateData", "SmtpPass", "SmsApiKey", "Password", "Pass" };

    /// <summary>خانواده‌های امنیتی/توکن — اصلاً در کاتالوگ نمی‌آیند (مستقل از اعلام ماژول‌ها).</summary>
    private static readonly HashSet<string> HiddenEntities = new(StringComparer.Ordinal)
    { "RefreshToken", "PasswordHistory", "PasswordReset", "PasswordResetToken" };

    /// <summary>نتیجهٔ کشف اعلام‌ها — ClrName ← (اعلام، کد ماژول مالک)؛ یک‌بار در نخستین مصرف.</summary>
    private static readonly Lazy<Dictionary<string, (HrDataExchangeDecl Decl, string Module)>> _decls = new(BuildDecls);

    private static Dictionary<string, (HrDataExchangeDecl, string)> BuildDecls()
    {
        var map = new Dictionary<string, (HrDataExchangeDecl, string)>(StringComparer.Ordinal);
        // اعلام هستهٔ پلتفرم (ماژول ذاتی — خانواده‌های شرکت/کاربر/نقش/اعلان/ممیزی/زبان)
        foreach (var d in Platform.Web.Modules.CoreModule.DataExchangeFamilies)
            map.TryAdd(d.Entity, (d, "core"));
        // اعلام پلاگین‌های دیتا — ماژولِ حاضر فقط خودش را اعلام می‌کند؛ هم‌نام (دو بستهٔ
        // موازی یک ماژول) = اولین کشف برنده (مثل خود مالکیت اسکیما در HrModuleRuntime)
        foreach (var dm in HrModuleRuntime.DataModules())
            foreach (var d in dm.DataExchange)
                map.TryAdd(d.Entity, (d, dm.Code));
        return map;
    }

    /// <summary>کد ماژول مالک یک خانوادهٔ اعلام‌شده — null = اعلام‌نشده.</summary>
    public static string? OwnerModule(string clrName) =>
        _decls.Value.TryGetValue(clrName, out var d) ? d.Item2 : null;

    /// <summary>یک خانوادهٔ داده در کاتالوگ هاب.</summary>
    public sealed record DataEntity(
        string Key, string Title, string Group, string ClrName, string Table,
        bool CanImport, string? BusinessKey, long Count, string[] Columns, string Module);

    private static IQueryable<object> SetOf(AppDbContext db, Type clr)
    {
        var mi = typeof(DataExchange).GetMethod(nameof(SetOfGeneric),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(clr);
        return (IQueryable<object>)mi.Invoke(null, new object[] { db })!;
    }

    private static IQueryable<object> SetOfGeneric<T>(AppDbContext db) where T : class => db.Set<T>();

    // ---------- درخت‌های عبارت برای انتخاب Id/کلید با EF (بدون بارگیری همهٔ ستون‌ها) ----------
    private static System.Linq.Expressions.Expression<Func<object, string>> IdSelector(Type clr)
    {
        var p = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
        var conv = System.Linq.Expressions.Expression.Convert(p, clr);
        var body = System.Linq.Expressions.Expression.Convert(
            System.Linq.Expressions.Expression.Property(conv, "Id"), typeof(string));
        return System.Linq.Expressions.Expression.Lambda<Func<object, string>>(body, p);
    }

    private static System.Linq.Expressions.Expression<Func<object, bool>> IdPredicate(Type clr, string id)
    {
        var p = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
        var conv = System.Linq.Expressions.Expression.Convert(p, clr);
        var body = System.Linq.Expressions.Expression.Equal(
            System.Linq.Expressions.Expression.Property(conv, "Id"),
            System.Linq.Expressions.Expression.Constant(id));
        return System.Linq.Expressions.Expression.Lambda<Func<object, bool>>(body, p);
    }

    /// <summary>"Id␁Key" در یک رشته — برای ایندکس کلید تجاری با یک کوئری سبک.</summary>
    private static System.Linq.Expressions.Expression<Func<object, string>> KeyPairSelector(Type clr, string key)
    {
        var p = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
        var conv = System.Linq.Expressions.Expression.Convert(p, clr);
        var id = System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.Property(conv, "Id"), typeof(string));
        var kv = System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.Property(conv, key), typeof(string));
        var concat = System.Linq.Expressions.Expression.Call(
            typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string), typeof(string) })!,
            id, System.Linq.Expressions.Expression.Constant("\u0001"), kv);
        return System.Linq.Expressions.Expression.Lambda<Func<object, string>>(concat, p);
    }

    /// <summary>
    /// کاتالوگ اعلامی‌محور — فقط خانواده‌های «اعلام‌شده» از ماژول‌های «حاضر و فعال».
    /// moduleEnabled = ملاک فعال‌بودن ماژول (ModSvc.Enabled)؛ null = همهٔ حاضران.
    /// </summary>
    public static List<DataEntity> Catalog(AppDbContext db, Func<string, bool>? moduleEnabled = null)
    {
        var list = new List<DataEntity>();
        foreach (var et in db.Model.GetEntityTypes())
        {
            if (et.ClrType.IsAbstract) continue;
            var name = et.ClrType.Name;
            if (HiddenEntities.Contains(name)) continue;
            // فقط خانواده‌های با جدول واقعی (owned types حذف می‌شوند)
            var table = et.GetTableName();
            if (string.IsNullOrEmpty(table)) continue;
            if (list.Any(x => x.Table == table)) continue; // جدول تکراری (discriminator) — یک‌بار

            // فاز ۱۲۲ — بدون اعلام ماژولِ حاضر، در هاب نیست (اعلام = ملاک نمایش)
            if (!_decls.Value.TryGetValue(name, out var reg)) continue;
            var (decl, module) = reg;
            if (moduleEnabled is not null && !moduleEnabled(module)) continue;

            var cols = et.GetProperties().Select(p => p.Name)
                .Where(n => !SecretColumns.Contains(n)).ToArray();
            if (cols.Length == 0) continue;

            long count;
            try { count = SetOf(db, et.ClrType).AsNoTracking().Count(); }
            catch { count = 0; }

            list.Add(new DataEntity(
                Key: name.ToLowerInvariant(),
                Title: decl.Title,
                Group: decl.Group,
                ClrName: name,
                Table: table,
                CanImport: decl.CanImport,
                BusinessKey: decl.BusinessKey,
                Count: count,
                Columns: cols,
                Module: module));
        }
        return list.OrderBy(x => x.Group).ThenBy(x => x.Title, StringComparer.Ordinal).ToList();
    }

    public static DataEntity? Find(AppDbContext db, string? key, Func<string, bool>? moduleEnabled = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return Catalog(db, moduleEnabled).FirstOrDefault(x => x.Key == key.Trim().ToLowerInvariant());
    }

    // ==================== برون‌بری ====================

    /// <summary>خواندن همهٔ ردیف‌های یک خانواده به شکل ستون→متن (همان قرارداد CSV/اکسل) یا JSON بومی.</summary>
    private static List<Dictionary<string, object?>> ReadRows(AppDbContext db, DataEntity ent, bool typed)
    {
        var clr = Type.GetType("Platform.Web.Domain." + ent.ClrName + ", Platform.Core")
                  ?? FindClrType(db, ent.ClrName);
        var piList = clr.GetProperties().Where(p => ent.Columns.Contains(p.Name))
            .Select(p => (p.Name, p)).ToArray();

        var rows = new List<Dictionary<string, object?>>();
        foreach (var obj in SetOf(db, clr).AsNoTracking())
        {
            var dict = new Dictionary<string, object?>(piList.Length, StringComparer.Ordinal);
            foreach (var (name, pi) in piList)
            {
                var v = pi.GetValue(obj);
                dict[name] = typed ? v : CellText(v);
            }
            rows.Add(dict);
            if (rows.Count >= MaxExportRows) break;
        }
        return rows;
    }

    private static Type FindClrType(AppDbContext db, string name) =>
        db.Model.GetEntityTypes().First(x => x.ClrType.Name == name).ClrType;

    /// <summary>متن امن سلول — تاریخ/عدد/بولین به قالب رفت‌وبرگشتی Invariant.</summary>
    private static string CellText(object? v)
    {
        if (v is null) return "";
        return v switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => v.ToString() ?? "",
        };
    }

    public static byte[] ExportXlsx(AppDbContext db, DataEntity ent)
    {
        var rows = ReadRows(db, ent, typed: false);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SheetName(ent.Title));
        ws.RightToLeft = HrT.Rtl;
        var cols = ent.Columns;
        for (var i = 0; i < cols.Length; i++) ws.Cell(1, i + 1).Value = cols[i];
        ws.Row(1).Style.Font.Bold = true;
        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < cols.Length; c++)
                ws.Cell(r + 2, c + 1).Value = rows[r].GetValueOrDefault(cols[c]) as string ?? "";
        ws.Columns().AdjustToContents(1, Math.Min(rows.Count + 1, 200));
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string SheetName(string title)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var s = new string(title.Where(ch => !invalid.Contains(ch)).ToArray());
        return string.IsNullOrWhiteSpace(s) ? "Data" : s.Length > 31 ? s[..31] : s;
    }

    public static byte[] ExportCsv(AppDbContext db, DataEntity ent)
    {
        var rows = ReadRows(db, ent, typed: false);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", ent.Columns.Select(EscapeCsv)));
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", ent.Columns.Select(c => EscapeCsv(r.GetValueOrDefault(c) as string ?? ""))));
        // BOM صریح — GetBytes خودش preamble نمی‌نویسد (اثبات‌شده در .NET 8)؛ اکسل فارسی بدون BOM خراب می‌شود
        var pre = new UTF8Encoding(true).GetPreamble();
        var body = new UTF8Encoding(false).GetBytes(sb.ToString());
        var bytes = new byte[pre.Length + body.Length];
        Array.Copy(pre, 0, bytes, 0, pre.Length);
        Array.Copy(body, 0, bytes, pre.Length, body.Length);
        return bytes;
    }

    private static string EscapeCsv(string s) =>
        s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r')
            ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] ExportJson(AppDbContext db, DataEntity ent)
    {
        var rows = ReadRows(db, ent, typed: true);
        // بسته‌بندی با متادیتا — مبدأ و زمان برای ردگیری مهاجرت
        var payload = new Dictionary<string, object?>
        {
            ["format"] = "hr-data-export",
            ["version"] = 1,
            ["entity"] = ent.ClrName,
            ["table"] = ent.Table,
            ["exportedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            ["rows"] = rows,
        };
        return JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
    }

    // ==================== قالب درون‌ریزی ====================

    public static byte[] ImportTemplate(DataEntity ent)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SheetName(ent.Title));
        ws.RightToLeft = HrT.Rtl;
        for (var i = 0; i < ent.Columns.Length; i++) ws.Cell(1, i + 1).Value = ent.Columns[i];
        ws.Row(1).Style.Font.Bold = true;
        var hintRow = ent.Columns.Length + 3;
        ws.Cell(hintRow, 1).Value = HrT.T("راهنما:") + " " + HrT.T("ستون‌ها دقیقاً نام ویژگی‌های سامانه‌اند — ساده‌ترین راه، ویرایش خروجی اکسل همین صفحه است.") + " " +
            (ent.BusinessKey is null
                ? HrT.T("ستون Id خالی = رکورد جدید.")
                : HrT.F("ستون Id خالی = رکورد جدید؛ رکورد موجود با ستون {0} پیدا و ویرایش می‌شود.", ent.BusinessKey));
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ==================== پارس فایل درون‌ریزی ====================

    /// <summary>نتیجهٔ پارس: ردیف‌های آمادهٔ ثبت + گزارش.</summary>
    public sealed class ParsedImport
    {
        public List<Dictionary<string, string?>> Rows { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string[] Columns { get; set; } = Array.Empty<string>();
        public string SourceFile { get; set; } = "";
    }

    public static ParsedImport ParseFile(DataEntity ent, Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        return ext switch
        {
            ".xlsx" or ".xls" => ParseXlsx(stream),
            ".csv" => ParseCsv(stream),
            ".json" => ParseJson(stream),
            _ => new ParsedImport { Errors = { HrT.T("قالب فایل پشتیبانی نمی‌شود — xlsx، csv یا json بفرستید") } },
        };
    }

    private static ParsedImport ParseXlsx(Stream stream)
    {
        var res = new ParsedImport();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var used = ws.RowsUsed().ToList();
            if (used.Count < 2)
            {
                res.Errors.Add(HrT.T("فایل خالی است — حداقل سربرگ و یک ردیف لازم است"));
                return res;
            }
            var header = used[0];
            var colCount = header.LastCellUsed()!.Address.ColumnNumber;
            var cols = Enumerable.Range(1, colCount).Select(c => header.Cell(c).GetString()?.Trim() ?? "").ToArray();
            res.Columns = cols.Where(c => c.Length > 0).ToArray();
            foreach (var row in used.Skip(1))
            {
                var cells = Enumerable.Range(1, colCount).Select(c => row.Cell(c).GetString()?.Trim() ?? "").ToArray();
                if (cells.All(string.IsNullOrWhiteSpace)) continue;
                var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
                for (var i = 0; i < cols.Length && i < cells.Length; i++)
                    if (cols[i].Length > 0) dict[cols[i]] = cells[i];
                res.Rows.Add(dict);
            }
        }
        catch (Exception ex)
        {
            res.Errors.Add(HrT.T("خواندن فایل اکسل ناموفق بود") + " (" + ex.Message + ")");
        }
        CheckLimit(res);
        return res;
    }

    private static ParsedImport ParseCsv(Stream stream)
    {
        var res = new ParsedImport();
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var raw = new List<string[]>();
            var field = new StringBuilder();
            var record = new List<string>();
            bool inQuotes = false;
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (!inQuotes) { record = new List<string>(); field.Clear(); }
                for (var i = 0; i < line.Length; i++)
                {
                    var ch = line[i];
                    if (inQuotes)
                    {
                        if (ch == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                            else inQuotes = false;
                        }
                        else field.Append(ch);
                    }
                    else if (ch == '"') inQuotes = true;
                    else if (ch == ',') { record.Add(field.ToString()); field.Clear(); }
                    else field.Append(ch);
                }
                if (inQuotes) continue; // ادامهٔ رکورد چندخطی
                record.Add(field.ToString());
                raw.Add(record.ToArray());
            }
            if (raw.Count < 2)
            {
                res.Errors.Add(HrT.T("فایل خالی است — حداقل سربرگ و یک ردیف لازم است"));
                return res;
            }
            var cols = raw[0].Select(c => c.Trim()).ToArray();
            res.Columns = cols.Where(c => c.Length > 0).ToArray();
            foreach (var cells in raw.Skip(1))
            {
                if (cells.All(string.IsNullOrWhiteSpace)) continue;
                var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
                for (var i = 0; i < cols.Length && i < cells.Length; i++)
                    if (cols[i].Length > 0) dict[cols[i]] = cells[i].Trim();
                res.Rows.Add(dict);
            }
        }
        catch (Exception ex)
        {
            res.Errors.Add(HrT.T("خواندن فایل CSV ناموفق بود") + " (" + ex.Message + ")");
        }
        CheckLimit(res);
        return res;
    }

    private static ParsedImport ParseJson(Stream stream)
    {
        var res = new ParsedImport();
        try
        {
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            // خروجی خود سامانه = شیء بسته‌بندی‌شده؛ آرایهٔ خام هم پذیرفته است
            JsonElement arr = root.ValueKind == JsonValueKind.Array ? root
                : root.TryGetProperty("rows", out var r) && r.ValueKind == JsonValueKind.Array ? r
                : throw new FormatException(HrT.T("ساختار JSON باید آرایه یا شیء دارای «rows» باشد"));
            var cols = new List<string>();
            var rows = new List<Dictionary<string, string?>>();
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var p in item.EnumerateObject())
                {
                    if (SecretColumns.Contains(p.Name)) { res.Warnings.Add(HrT.F("ستون حساس «{0}» نادیده گرفته شد", p.Name)); continue; }
                    if (!cols.Contains(p.Name)) cols.Add(p.Name);
                    dict[p.Name] = p.Value.ValueKind switch
                    {
                        JsonValueKind.String => p.Value.GetString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => null,
                        _ => p.Value.GetRawText(),
                    };
                }
                rows.Add(dict);
            }
            res.Columns = cols.ToArray();
            res.Rows = rows;
        }
        catch (Exception ex)
        {
            res.Errors.Add(HrT.T("خواندن JSON ناموفق بود") + " (" + ex.Message + ")");
        }
        CheckLimit(res);
        return res;
    }

    private static void CheckLimit(ParsedImport res)
    {
        if (res.Rows.Count > MaxImportRows)
            res.Errors.Insert(0, HrT.F("حداکثر {0} ردیف در هر فایل درون‌ریزی مجاز است (فایل: {1} ردیف)", HrT.Num(MaxImportRows), HrT.Num(res.Rows.Count)));
    }

    // ==================== ثبت ====================

    public sealed class CommitReport
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// ثبت ردیف‌های پارس‌شده — هم‌آوری: ردیف با Id موجود ویرایش می‌شود؛
    /// وگرنه با کلید تجاری (مثلاً PersonnelCode)؛ وگرنه رکورد جدید. ستون‌های
    /// ناشناخته ثبت را متوقف می‌کنند و هر ردیف خطای خودش را در گزارش می‌نویسد.
    /// PasswordHash فقط از JSON با هش bcrypt معتبر سامانه پذیرفته می‌شود (مهاجرت رفت‌وبرگشتی).
    /// </summary>
    public static CommitReport Commit(AppDbContext db, DataEntity ent, List<Dictionary<string, string?>> rows, bool allowPasswordHash = false)
    {
        var report = new CommitReport();
        var clr = FindClrType(db, ent.ClrName);
        var props = clr.GetProperties().ToDictionary(p => p.Name, StringComparer.Ordinal);
        var et = db.Model.GetEntityTypes().First(x => x.ClrType.Name == ent.ClrName);
        var scalarNames = et.GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var idPi = props.TryGetValue("Id", out var ip) ? ip : null;

        // ستون‌های ناشناخته = قالب غلط — ثبت نکن (همان محافظتِ ورود گروهی پرسنل)
        var unknown = rows.SelectMany(r => r.Keys).Where(k => !scalarNames.Contains(k)).Distinct().Take(10).ToList();
        foreach (var u in unknown)
            report.Errors.Add(HrT.F("ستون «{0}» در این خانوادهٔ داده وجود ندارد — ستون‌های معتبر = سربرگ قالب یا خروجی اکسل همین صفحه", u));
        if (unknown.Count > 0) return report;

        // ایندکس Id + کلید تجاری موجود (دو کوئری سبک فقط روی ستون‌های لازم)
        var existingIds = SetOf(db, clr).AsNoTracking().Select(IdSelector(clr)).ToList()
            .Where(s => !string.IsNullOrEmpty(s)).ToHashSet(StringComparer.Ordinal);

        List<(string Val, string Id)> bkIndex = new();
        var bk = ent.BusinessKey;
        var hasBk = !string.IsNullOrEmpty(bk) && props.ContainsKey(bk) && scalarNames.Contains(bk);
        if (hasBk)
            bkIndex = SetOf(db, clr).AsNoTracking().Select(KeyPairSelector(clr, bk!)).ToList()
                .Select(s => s.Split('\u0001')).Where(a => a.Length == 2 && a[0].Length > 0 && a[1].Length > 0)
                .Select(a => (Val: a[1], Id: a[0])).ToList();

        var loaded = new Dictionary<string, object?>(StringComparer.Ordinal); // کش موجودی‌های بازیابی‌شده
        object? FindById(string id)
        {
            if (loaded.TryGetValue(id, out var hit)) return hit;
            var e = SetOf(db, clr).FirstOrDefault(IdPredicate(clr, id));
            if (e is not null) loaded[id] = e;
            return e;
        }

        var seenBk = new HashSet<string>(StringComparer.Ordinal);
        var pending = new List<(object Entity, bool IsNew)>();
        var labels = new List<string>();

        foreach (var (row, idx) in rows.Select((r, i) => (r, i + 2)))
        {
            try
            {
                // مرحلهٔ ۱ — همهٔ ستون‌ها را «اول تبدیل کن»؛ هیچ جهشی روی موجودی‌ها تا تبدیل کامل (صفر نیم‌ویرایش)
                var applied = new List<(System.Reflection.PropertyInfo Pi, object? Val)>();
                bool rowError = false;
                foreach (var (col, rawVal) in row)
                {
                    // فاز ۱۱۲ — ستون Id در مرحلهٔ یافتن رکورد مدیریت می‌شود؛ اعمال مستقیم آن
                    // (سلول خالی) کلید اصلی را null می‌کند — ممنوع
                    if (col == "Id") continue;
                    if (!props.TryGetValue(col, out var pi) || !pi.CanWrite) continue;
                    if (!scalarNames.Contains(col)) continue;
                    if (SecretColumns.Contains(col))
                    {
                        // PasswordHash فقط در JSON معتبر (هاش bcrypt از بستهٔ خود سامانه)
                        if (col != "PasswordHash" || !allowPasswordHash || !IsBcryptHash(rawVal)) continue;
                    }
                    if (!TryConvert(rawVal, pi.PropertyType, out var converted, out var err))
                    {
                        report.Errors.Add(HrT.F("ردیف {0}: مقدار ستون «{1}» قابل تبدیل به {2} نیست: {3}", HrT.Num(idx), col, FmtTypeName(pi.PropertyType), err));
                        rowError = true; break;
                    }
                    applied.Add((pi, converted));
                }
                if (rowError) continue;

                var rowId = (row.GetValueOrDefault("Id") ?? "").Trim();

                // مرحلهٔ ۲ — یافتن رکورد مقصد: Id ← کلید تجاری ← رکورد جدید
                object? target = null;
                bool isNew = true;
                if (rowId.Length > 0 && existingIds.Contains(rowId))
                {
                    target = FindById(rowId);
                    if (target is not null) isNew = false;
                }
                if (target is null && hasBk && (row.GetValueOrDefault(bk!) ?? "").Trim().Length > 0)
                {
                    var bv = (row.GetValueOrDefault(bk!) ?? "").Trim();
                    if (!seenBk.Add(bv))
                    {
                        report.Skipped++;
                        report.Errors.Add(HrT.F("ردیف {0}: کلید «{1}» در فایل تکراری است — فقط اولین ردیف ثبت شد", HrT.Num(idx), bv));
                        continue;
                    }
                    var hit = bkIndex.FirstOrDefault(x => x.Val == bv);
                    // فاز ۱۱۲ — جدول خالی = default(ValueTuple) با Id=null — مقایسهٔ امنِ رشته‌ای
                    if (!string.IsNullOrEmpty(hit.Id))
                    {
                        target = FindById(hit.Id);
                        if (target is not null) isNew = false;
                    }
                }
                if (target is null)
                {
                    target = Activator.CreateInstance(clr)!;
                    isNew = true;
                    if (rowId.Length > 0 && idPi is { CanWrite: true }) idPi.SetValue(target, rowId);
                }

                // مرحلهٔ ۳ — اعمال مقادیر + اعتبارسنجی کلید تجاری
                foreach (var (pi, val) in applied) pi.SetValue(target, val);
                if (hasBk && props[bk!].GetValue(target) is string b2 && b2.Trim().Length == 0)
                {
                    report.Skipped++;
                    report.Errors.Add(HrT.F("ردیف {0}: کلید تجاری «{1}» خالی است — ردیف نادیده گرفته شد", HrT.Num(idx), bk));
                    if (isNew) db.Entry(target).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                    continue;
                }
                if (isNew) db.Add(target);
                if (rowId.Length > 0) existingIds.Add(rowId);
                if (hasBk && props[bk!].GetValue(target) is string b3 && b3.Trim().Length > 0 && idPi?.GetValue(target) is string nid && nid.Length > 0)
                    bkIndex.Add((b3.Trim(), nid));
                pending.Add((target, isNew));
                labels.Add(rowId.Length > 0 ? rowId : hasBk ? (props[bk!].GetValue(target)?.ToString() ?? $"#{idx}") : $"#{idx}");
            }
            catch (Exception ex)
            {
                // فاز ۱۱۲ — تشخیص: نوع ریشه + چند فریم بالای پشته در گزارش
                var root = ex.GetBaseException();
                var top = string.Join(" <- ", (new System.Diagnostics.StackTrace(root, false).GetFrames() ?? [])
                    .Take(4).Select(f => $"{f.GetMethod()?.DeclaringType?.Name}.{f.GetMethod()?.Name}:{f.GetFileLineNumber()}"));
                report.Errors.Add(HrT.F("ردیف {0}: {1} [{2}]", HrT.Num(idx), root.Message, top));
            }
        }

        try
        {
            db.SaveChanges();
            report.Inserted = pending.Count(p => p.IsNew);
            report.Updated = pending.Count(p => !p.IsNew);
        }
        catch (Exception ex)
        {
            report.Errors.Add(HrT.T("ثبت در بانک ناموفق بود — هیچ ردیفی ذخیره نشد") + " (" + ex.Message + ")");
            foreach (var p in pending)
                if (p.IsNew) db.Entry(p.Entity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }
        return report;
    }

    private static bool IsBcryptHash(string? s) =>
        !string.IsNullOrEmpty(s) && (s.StartsWith("$2a$") || s.StartsWith("$2b$") || s.StartsWith("$2y$"));

    // ---- تبدیل مقدار سلول به نوع ویژگی ----
    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd",
    };

    private static string ConvertFaDigits(string s) => new string(s.Select(c =>
        c >= '۰' && c <= '۹' ? (char)(c - '۰' + '0') :
        c >= '٠' && c <= '٩' ? (char)(c - '٠' + '0') : c).ToArray());

    public static bool TryConvert(string? raw, Type target, out object? value, out string error)
    {
        value = null;
        error = "";
        var s = (raw ?? "").Trim();
        var t = Nullable.GetUnderlyingType(target) ?? target;

        if (string.IsNullOrEmpty(s))
        {
            // nullable → null؛ غیر-nullable → مقدار پیش‌فرض (خاموش‌کردن فیلد با سلول خالی)
            value = t.IsValueType ? Activator.CreateInstance(t) : null;
            return true;
        }
        s = ConvertFaDigits(s);

        try
        {
            if (t == typeof(string)) { value = s; return true; }
            if (t == typeof(bool))
            {
                value = s.ToLowerInvariant() is "true" or "1" or "yes" or "y" or "بله" or "فعال" or "on";
                return true;
            }
            if (t == typeof(int)) { value = int.Parse(s, System.Globalization.CultureInfo.InvariantCulture); return true; }
            if (t == typeof(long)) { value = long.Parse(s, System.Globalization.CultureInfo.InvariantCulture); return true; }
            if (t == typeof(short)) { value = short.Parse(s, System.Globalization.CultureInfo.InvariantCulture); return true; }
            if (t == typeof(decimal))
            {
                value = decimal.Parse(s.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            if (t == typeof(double)) { value = double.Parse(s.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture); return true; }
            if (t == typeof(float)) { value = float.Parse(s.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture); return true; }
            if (t == typeof(Guid)) { value = Guid.Parse(s); return true; }
            if (t == typeof(DateTime))
            {
                // سال ۱۳۰۰–۱۵۰۰ = جلالی (مثل 1404/07/26) — مهاجرت از فایل‌های شمسی‌محور
                var m = System.Text.RegularExpressions.Regex.Match(s, @"^(\d{4})[\/\-\.](\d{1,2})[\/\-\.](\d{1,2})");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var yy) && yy >= 1300 && yy <= 1500)
                {
                    value = Jalali.FromJalali(yy, int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
                    return true;
                }
                if (DateTime.TryParseExact(s, DateFormats, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dt)) { value = dt; return true; }
                if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dt2)) { value = dt2; return true; }
                error = HrT.T("تاریخ نامعتبر (الگو: 2026-03-21 یا 1404/07/26)");
                return false;
            }
            value = Convert.ChangeType(s, t, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    private static string FmtTypeName(Type t) => Nullable.GetUnderlyingType(t) is { } u ? u.Name : t.Name;
}

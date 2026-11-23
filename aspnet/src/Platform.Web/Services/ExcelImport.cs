using ClosedXML.Excel;
using Platform.Web.Data;
using Platform.Web.Domain;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Platform.Web.Services;

// ============================================================
// ورود گروهی از اکسل (PLAN Phase 16)
// قالب استاندارد ۲ برگی: «پرسنل» + «تردد تاریخی»
// اعتبارسنجی: کد ملی (چک‌سام دیجیتال)، تکراری‌ها (فایل/دیتابیس)، فیلدهای الزامی
// پیش‌نمایش + گزارش خطا + ثبت نهایی
// ============================================================
public static class ExcelImport
{
    public const int MaxRows = 500;

    public static byte[] BuildTemplate()
    {
        using var wb = new XLWorkbook();
        // فاز ۸۱ — نام شیت‌ها قرارداد قالب است و در ParsePeople/ParseAttendance هم با HrT.T خوانده می‌شود
        var ws = wb.Worksheets.Add(HrT.T("پرسنل"));
        var headers = new[] { HrT.T("کد پرسنلی*"), HrT.T("نام*"), HrT.T("نام خانوادگی*"), HrT.T("کد ملی*"), HrT.T("نوع استخدام"), HrT.T("کد واحد سازمانی"), HrT.T("تلفن"), HrT.T("ایمیل"), HrT.T("حقوق پایه"), HrT.T("تاریخ استخدام (1404/01/01)"), HrT.T("سهمیه مرخصی سالانه"), HrT.T("سهمیه مرخصی استحقاقی") };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
        var samples = new object?[][]
        {
            new object?[] { "2001", "علی", "محمدی", "0012345679", "قراردادی", "IT", "09121234567", "ali@example.com", 25000000, "1404/01/15", 30, 12 },
            new object?[] { "2002", "سارا", "کریمی", "0065123451", "رسمی", "", "09127654321", "", 32000000, "1403/07/01", 30, 12 },
        };
        for (var r = 0; r < samples.Length; r++)
            for (var c = 0; c < samples[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(samples[r][c]);
        ws.Columns().AdjustToContents();

        var wa = wb.Worksheets.Add(HrT.T("تردد تاریخی"));
        var ha = new[] { HrT.T("کد پرسنلی*"), HrT.T("تاریخ (1404/05/01)"), HrT.T("ساعت ورود (07:45)"), HrT.T("ساعت خروج (16:30)") };
        for (var i = 0; i < ha.Length; i++) wa.Cell(1, i + 1).Value = ha[i];
        wa.Row(1).Style.Font.Bold = true;
        var sa = new object?[][]
        {
            new object?[] { "1001", "1404/05/01", "07:45", "16:30" },
            new object?[] { "1001", "1404/05/02", "08:10", "17:00" },
        };
        for (var r = 0; r < sa.Length; r++)
            for (var c = 0; c < sa[r].Length; c++)
                wa.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(sa[r][c]);
        wa.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ---------- اعتبارسنجی کد ملی ایران ----------
    public static bool IsValidNationalId(string? nid)
    {
        if (string.IsNullOrWhiteSpace(nid)) return false;
        nid = nid.Trim().ConvertFaDigits();
        if (!Regex.IsMatch(nid, @"^\d{10}$")) return false;
        if (nid.All(c => c == nid[0])) return false;
        var check = nid[9] - '0';
        var sum = Enumerable.Range(0, 9).Sum(i => (nid[i] - '0') * (10 - i));
        var rem = sum % 11;
        return rem < 2 ? check == rem : check == 11 - rem;
    }

    // ---------- پرسنل ----------
    public class PersonRow
    {
        public string PersonnelCode { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string NationalId { get; set; } = "";
        public string EmploymentType { get; set; } = "قراردادی";
        public string? DeptCode { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public decimal BaseSalary { get; set; }
        public string HireDateJ { get; set; } = "";   // جلالی برای نمایش
        public DateTime HireDate { get; set; }
        public int AnnualLeaveEnt { get; set; } = 30;
        public int SickLeaveEnt { get; set; } = 12;
    }

    public class ImportResult<T>
    {
        public List<T> Rows { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    private static readonly string[] EmploymentTypes = { "رسمی", "قراردادی", "پیمانکاری", "اقماری", "کارآموز" };

    public static ImportResult<PersonRow> ParsePeople(Stream stream, AppDbContext db)
    {
        var res = new ImportResult<PersonRow>();
        List<string[]> rows;
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(HrT.T("پرسنل"));
            // مقادیر باید داخل اسکوپ کاربرگ استخراج شوند (بعد از dispose زنده نمی‌مانند)
            rows = ws.RowsUsed().Skip(1)
                .Select(r => Enumerable.Range(1, 12).Select(c => r.Cell(c).GetString()?.Trim() ?? "").ToArray())
                .ToList();
        }
        catch (Exception ex)
        {
            res.Errors.Add(HrT.T("خواندن فایل ناموفق بود — قالب استاندارد را از دکمه «دانلود قالب» بگیرید. (") + ex.Message + ")");
            return res;
        }
        if (rows.Count > MaxRows) { res.Errors.Add(HrT.F("حداکثر {0} ردیف در هر بار ورود مجاز است (فایل: {1})", HrT.Num(MaxRows), HrT.Num(rows.Count))); return res; }

        var seenCodes = new HashSet<string>();
        var seenNids = new HashSet<string>();
        var depts = db.Set<Department>().ToList();
        var existingCodes = db.Set<Employee>().Select(e => e.PersonnelCode).ToHashSet();
        var existingNids = db.Set<Employee>().Select(e => e.NationalId).ToHashSet();

        for (var idx = 0; idx < rows.Count; idx++)
        {
            var cells = rows[idx];
            var n = idx + 2; // شماره ردیف اکسل
            var code = cells[0];
            var first = cells[1];
            var last = cells[2];
            var nid = cells[3].ConvertFaDigits();
            var et = cells[4];
            var deptCode = cells[5];
            var phone = cells[6].ConvertFaDigits();
            var email = cells[7];
            var salary = cells[8];
            var hire = cells[9].ConvertFaDigits();
            var annual = cells[10];
            var sick = cells[11];

            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)) continue; // ردیف خالی

            if (string.IsNullOrWhiteSpace(code)) { res.Errors.Add(HrT.F("ردیف {0}: کد پرسنلی الزامی است", HrT.Num(n))); continue; }
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last)) { res.Errors.Add(HrT.F("ردیف {0}: نام و نام خانوادگی الزامی است", HrT.Num(n))); continue; }
            if (!IsValidNationalId(nid)) { res.Errors.Add(HrT.F("ردیف {0}: کد ملی «{1}» نامعتبر است (الگوی ۱۰ رقمی و چک‌سام)", HrT.Num(n), nid)); continue; }
            if (!seenCodes.Add(code)) { res.Errors.Add(HrT.F("ردیف {0}: کد پرسنلی «{1}» در فایل تکراری است", HrT.Num(n), code)); continue; }
            if (!seenNids.Add(nid)) { res.Errors.Add(HrT.F("ردیف {0}: کد ملی «{1}» در فایل تکراری است", HrT.Num(n), nid)); continue; }
            if (existingCodes.Contains(code)) { res.Errors.Add(HrT.F("ردیف {0}: کد پرسنلی «{1}» قبلاً در سامانه ثبت شده", HrT.Num(n), code)); continue; }
            if (existingNids.Contains(nid)) { res.Errors.Add(HrT.F("ردیف {0}: کد ملی «{1}» قبلاً در سامانه ثبت شده", HrT.Num(n), nid)); continue; }

            var employment = EmploymentTypes.Contains(et) ? et : "قراردادی";
            if (!string.IsNullOrWhiteSpace(et) && !EmploymentTypes.Contains(et))
                res.Warnings.Add(HrT.F("ردیف {0}: نوع استخدام «{1}» شناخته نشد — «قراردادی» درج شد", HrT.Num(n), et));

            DateTime hireDate;
            if (string.IsNullOrWhiteSpace(hire))
            {
                hireDate = DateTime.Now;
                res.Warnings.Add(HrT.F("ردیف {0}: تاریخ استخدام خالی — امروز درج شد", HrT.Num(n)));
            }
            else if (!TryParseJalali(hire, out hireDate))
            {
                res.Errors.Add(HrT.F("ردیف {0}: تاریخ استخدام «{1}» نامعتبر است (الگو: 1404/01/15)", HrT.Num(n), hire));
                continue;
            }

            string? deptId = null;
            if (!string.IsNullOrWhiteSpace(deptCode))
            {
                var d = depts.FirstOrDefault(x => x.Code == deptCode) ?? depts.FirstOrDefault(x => x.Name == deptCode);
                if (d is null) res.Warnings.Add(HrT.F("ردیف {0}: واحد سازمانی «{1}» یافت نشد — بدون واحد درج شد", HrT.Num(n), deptCode));
                else deptId = d.Id;
            }

            res.Rows.Add(new PersonRow
            {
                PersonnelCode = code,
                FirstName = first,
                LastName = last,
                NationalId = nid,
                EmploymentType = employment,
                DeptCode = deptId,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                BaseSalary = decimal.TryParse(salary, NumberStyles.Any, CultureInfo.InvariantCulture, out var s) ? s : 0,
                HireDate = hireDate,
                HireDateJ = Jalali.Fmt(hireDate),
                AnnualLeaveEnt = int.TryParse(annual, out var a) ? a : 30,
                SickLeaveEnt = int.TryParse(sick, out var k) ? k : 12,
            });
        }
        return res;
    }

    // ---------- تردد تاریخی ----------
    public class AttendanceRow
    {
        public string EmployeeId { get; set; } = "";
        public string PersonnelCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string Date { get; set; } = "";
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int WorkMinutes { get; set; }
    }

    public static ImportResult<AttendanceRow> ParseAttendance(Stream stream, AppDbContext db)
    {
        var res = new ImportResult<AttendanceRow>();
        List<string[]> rows;
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(HrT.T("تردد تاریخی"));
            rows = ws.RowsUsed().Skip(1)
                .Select(r => Enumerable.Range(1, 4).Select(c => r.Cell(c).GetString()?.Trim() ?? "").ToArray())
                .ToList();
        }
        catch (Exception ex)
        {
            res.Errors.Add(HrT.T("خواندن فایل ناموفق بود. (") + ex.Message + ")");
            return res;
        }
        if (rows.Count > MaxRows) { res.Errors.Add(HrT.F("حداکثر {0} ردیف در هر بار ورود مجاز است", HrT.Num(MaxRows))); return res; }

        var byCode = db.Set<Employee>().ToDictionary(e => e.PersonnelCode, e => e);
        var seen = new HashSet<string>();
        var existing = ExistingAttKeysSafe(db);

        for (var idx = 0; idx < rows.Count; idx++)
        {
            var cells = rows[idx];
            var n = idx + 2;
            var code = cells[0];
            var dateJ = cells[1].ConvertFaDigits();
            var tIn = cells[2].ConvertFaDigits();
            var tOut = cells[3].ConvertFaDigits();
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(dateJ)) continue;

            if (!byCode.TryGetValue(code, out var emp)) { res.Errors.Add(HrT.F("ردیف {0}: کد پرسنلی «{1}» در سامانه نیست (اول پرسنل را وارد کنید)", HrT.Num(n), code)); continue; }
            if (!TryParseJalali(dateJ, out var d)) { res.Errors.Add(HrT.F("ردیف {0}: تاریخ «{1}» نامعتبر است", HrT.Num(n), dateJ)); continue; }
            if (!TimeSpan.TryParseExact(tIn, @"hh\:mm", CultureInfo.InvariantCulture, out var tin) ||
                !TimeSpan.TryParseExact(tOut, @"hh\:mm", CultureInfo.InvariantCulture, out var tout))
            { res.Errors.Add(HrT.F("ردیف {0}: ساعت ورود/خروج نامعتبر (الگو: 07:45)", HrT.Num(n))); continue; }

            var dateKey = d.ToString("yyyy-MM-dd");
            var key = emp.Id + "|" + dateKey;
            if (!seen.Add(key)) { res.Warnings.Add(HrT.F("ردیف {0}: تردد تکراری {1} در {2} — نادیده گرفته شد", HrT.Num(n), code, dateJ)); continue; }
            if (existing.Contains(key)) { res.Warnings.Add(HrT.F("ردیف {0}: برای {1} در {2} رکورد موجود است — نادیده گرفته شد", HrT.Num(n), code, dateJ)); continue; }

            var ci = d.Add(tin);
            var co = d.Add(tout);
            if (co <= ci) co = co.AddDays(1); // شیفت شب
            res.Rows.Add(new AttendanceRow
            {
                EmployeeId = emp.Id, PersonnelCode = code, Name = $"{emp.FirstName} {emp.LastName}",
                Date = dateKey, CheckIn = ci, CheckOut = co,
                WorkMinutes = (int)(co - ci).TotalMinutes,
            });
        }
        return res;
    }

    // ---------- کمکی‌ها ----------
    public static string ConvertFaDigits(this string s) => new string(s.Select(c =>
        c >= '۰' && c <= '۹' ? (char)(c - '۰' + '0') :
        c >= '٠' && c <= '٩' ? (char)(c - '٠' + '0') : c).ToArray());

    public static bool TryParseJalali(string s, out DateTime dt)
    {
        dt = default;
        var m = Regex.Match(s, @"^(\d{4})[\/\-.](\d{1,2})[\/\-.](\d{1,2})$");
        if (!m.Success) return false;
        try
        {
            dt = Jalali.FromJalali(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
            return true;
        }
        catch { return false; }
    }

    // ---------- ثبت نهایی ----------
    public static (int inserted, int skipped) CommitPeople(AppDbContext db, string json, string companyId)
    {
        var rows = JsonSerializer.Deserialize<List<PersonRow>>(json) ?? new();
        var inserted = 0;
        var existingCodes = db.Set<Employee>().Select(e => e.PersonnelCode).ToHashSet();
        foreach (var r in rows)
        {
            if (existingCodes.Contains(r.PersonnelCode)) continue; // idempotent
            db.Set<Employee>().Add(new Employee
            {
                PersonnelCode = r.PersonnelCode,
                FirstName = r.FirstName,
                LastName = r.LastName,
                NationalId = r.NationalId,
                EmploymentType = r.EmploymentType,
                DepartmentId = string.IsNullOrWhiteSpace(r.DeptCode) ? null : r.DeptCode,
                Phone = r.Phone,
                Email = r.Email,
                BaseSalary = r.BaseSalary,
                HireDate = r.HireDate,
                AnnualLeaveEnt = r.AnnualLeaveEnt,
                SickLeaveEnt = r.SickLeaveEnt,
                CompanyId = companyId,
            });
            existingCodes.Add(r.PersonnelCode);
            inserted++;
        }
        db.SaveChanges();
        return (inserted, rows.Count - inserted);
    }

    /// <summary>ثبت ترددهای ورودگروهی — فاز ۶۸: commit ∈ پلاگین حضور (قرارداد IAttendanceGateway)؛ بدون آن (0,0).</summary>
    public static (int inserted, int skipped) CommitAttendance(AppDbContext db, string json, IServiceProvider sp)
    {
        var rows = JsonSerializer.Deserialize<List<HrAttImportRow>>(json) ?? new();
        return sp.GetRequiredService<Platform.Contracts.IAttendanceGateway>().ImportRowsAsync(db, rows).GetAwaiter().GetResult();
    }

#if HR_MODULE_ATTENDANCE
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (int inserted, int skipped) CommitAttendanceCore(AppDbContext db, string json)
    {
        var rows = JsonSerializer.Deserialize<List<AttendanceRow>>(json) ?? new();
        var inserted = 0; var skipped = 0;
        var existing = ExistingAttKeysSafe(db);
        foreach (var r in rows)
        {
            if (!existing.Add(r.EmployeeId + "|" + r.Date)) { skipped++; continue; }
            db.Set<AttendanceRecord>().Add(new AttendanceRecord
            {
                EmployeeId = r.EmployeeId, Date = r.Date, CheckIn = r.CheckIn, CheckOut = r.CheckOut,
                WorkMinutes = r.WorkMinutes, Status = "حاضر",
            });
            inserted++;
        }
        db.SaveChanges();
        return (inserted, skipped);
    }
#endif
    // ---------- فاز ۶۷ — کلیدهای تردد موجود ∈ ماژول حضور؛ بدون لایهٔ دیتای آن مجموعهٔ خالی ----------
#if HR_MODULE_ATTENDANCE
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static HashSet<string> ExistingAttKeys(AppDbContext db) =>
        db.Set<AttendanceRecord>().Select(a => a.EmployeeId + "|" + a.Date).ToHashSet();
#endif
    private static HashSet<string> ExistingAttKeysSafe(AppDbContext db)
    {
#if HR_MODULE_ATTENDANCE
        if (Platform.Contracts.HrModulePresence.DataInstalled("attendance")) return ExistingAttKeys(db);
#endif
        return new HashSet<string>();
    }

}

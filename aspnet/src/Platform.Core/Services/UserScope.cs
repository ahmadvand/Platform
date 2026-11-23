using Platform.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Web.Services;

// ============================================================
// فاز ۷۴ — نوع‌های دامنهٔ رکوردی (Row-Level) در پلتفرم مشترک
// ----------------------------------------------------------------------
// «UserScope» خروجی حل دامنهٔ کاربر است: اجتماع دامنه‌های نقش‌های او
// (شرکت‌ها / واحدها با زیرشاخه / شناسه‌های پرسنل / خودم) و سقف شرکت کاربر.
// «ScopeQuery» افزونه‌های کوئری‌اند — predicate ترجمه‌پذیر به SQL روی
// ستون‌های ایندکس‌دار (CompanyId/DepartmentId/EmployeeId) تا گزارش‌ها همان
// یک کوئری قبلی بمانند و فقط WHERE تنگ‌تر شود (پاسخ به «سرعت و گزارش‌ها»).
// لیست‌های بزرگ شناسهٔ پرسنل برای سقف پارامتر SQL Server (۲۱۰۰) تکه‌تکه می‌شوند.
// «IUserScopeProvider» قرارداد حل دامنه است — پیاده‌سازی: DataScopeService هاست
// (به composite context نیاز دارد)؛ ماژول‌ها از افزونهٔ ScopeAsync کنترلر استفاده می‌کنند.
// ============================================================

public sealed record UserScope(
    bool All,
    HashSet<string> Companies,
    HashSet<string> Depts,
    HashSet<string>? EmpIds,
    string SelfEmpId,
    string Summary)
{
    /// <summary>دامنهٔ کامل — بدون فیلتر (superadmin یا نقش بدون سطر دامنه).</summary>
    public static readonly UserScope Unrestricted =
        new(true, [], [], null, "", "همهٔ داده‌ها");

    public bool EmpAllowed(string employeeId) =>
        All || (EmpIds?.Contains(employeeId) ?? true);

    public bool CoAllowed(string? companyId) =>
        All || (!string.IsNullOrEmpty(companyId) && Companies.Contains(companyId));
}

/// <summary>قرارداد حل دامنهٔ رکوردی کاربر — پیاده‌سازی در هاست (DataScopeService).</summary>
public interface IUserScopeProvider
{
    Task<UserScope> ForUserAsync(string userId, bool isSuperadmin);
}

/// <summary>برچسب‌های فارسی سطوح/انواع دامنه — مشترک بین ویوها و پیام‌ها.</summary>
public static class Scope
{
    /// <summary>برچسب سطح عددی قدیمی Roles.DataScope (۹/۳/۲/۱) — برای سازگاری نمایش.</summary>
    public static string TitleFa(int level) => level switch
    {
        3 => "شرکتِ خودم",
        2 => "واحد سازمانی خودم",
        1 => "فقط رکوردهای خودم",
        _ => "همهٔ داده‌ها",
    };

    /// <summary>برچسب نوع سطر RoleScope.</summary>
    public static string KindFa(string kind) => kind switch
    {
        "company" => "شرکت‌های انتخابی",
        "dept" => "واحد سازمانی انتخابی (با زیرشاخه‌ها)",
        "self" => "فقط رکوردهای خودم",
        _ => "همهٔ داده‌ها — بدون محدودیت",
    };
}

// توجه: افزونهٔ کنترلری «ScopeAsync» ∈ Infrastructure/ScopeOf.cs (ScopeOfExt)
// — تک‌تعریف کانونی؛ از IUserScopeProvider همین فایل استفاده می‌کند.

public static class ScopeQuery
{
    private static readonly System.Reflection.MethodInfo ContainsMethod =
        typeof(HashSet<string>).GetMethod(nameof(HashSet<string>.Contains), [typeof(string)])!;

    private const int ChunkSize = 400;   // سقف امن پارامترها برای SQL Server (۲۱۰۰)

    /// <summary>فیلتر ردیف‌های کلیدشده با شناسهٔ پرسنل (AttendanceRecord/Payslip/LeaveRequest/…).</summary>
    public static IQueryable<T> WhereEmpScope<T>(this IQueryable<T> q, UserScope s,
        System.Linq.Expressions.Expression<Func<T, string>> empIdSel)
    {
        if (s.All) return q;
        var ids = s.EmpIds;
        if (ids is null) return q;   // org غایب — جدول‌های وابسته به پرسنل هم غایب‌اند
        if (ids.Count == 0) return q.Where(_ => false);

        var chunks = ids.Chunk(ChunkSize);
        System.Linq.Expressions.Expression? body = null;
        var pe = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
        foreach (var chunk in chunks)
        {
            var set = chunk.ToHashSet();
            var contains = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Constant(set), ContainsMethod,
                System.Linq.Expressions.Expression.Invoke(empIdSel, pe));
            body = body is null ? contains : System.Linq.Expressions.Expression.OrElse(body, contains);
        }
        return q.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body!, pe));
    }

    /// <summary>فیلتر ردیف‌های کلیدشده با شناسهٔ شرکت (PayrollRun/…).</summary>
    public static IQueryable<T> WhereCoScope<T>(this IQueryable<T> q, UserScope s,
        System.Linq.Expressions.Expression<Func<T, string?>> coIdSel)
    {
        if (s.All) return q;
        if (s.Companies.Count == 0) return q.Where(_ => false);

        var pe = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
        var contains = System.Linq.Expressions.Expression.Call(
            System.Linq.Expressions.Expression.Constant(s.Companies), ContainsMethod,
            System.Linq.Expressions.Expression.Invoke(coIdSel, pe));
        return q.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(contains, pe));
    }

    /// <summary>فیلتر ستونیِ پرسنل (خودِ Employees — CompanyId/DepartmentId ایندکس‌دار؛ بدون لیست شناسه).</summary>
    public static IQueryable<Employee> WhereEmpTableScope(this IQueryable<Employee> q, UserScope s)
    {
        if (s.All) return q;
        var pe = System.Linq.Expressions.Expression.Parameter(typeof(Employee), "e");
        System.Linq.Expressions.Expression? body = null;
        if (s.Companies.Count > 0)
        {
            var contains = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Constant(s.Companies), ContainsMethod,
                System.Linq.Expressions.Expression.Property(pe, nameof(Employee.CompanyId)));
            body = contains;
        }
        if (s.Depts.Count > 0)
        {
            var deptContains = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Constant(s.Depts), ContainsMethod,
                System.Linq.Expressions.Expression.Property(pe, nameof(Employee.DepartmentId)));
            body = body is null ? deptContains : System.Linq.Expressions.Expression.OrElse(body, deptContains);
        }
        if (!string.IsNullOrEmpty(s.SelfEmpId))
        {
            var eq = System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Property(pe, nameof(Employee.Id)),
                System.Linq.Expressions.Expression.Constant(s.SelfEmpId));
            body = body is null ? eq : System.Linq.Expressions.Expression.OrElse(body, eq);
        }
        if (body is null) return q.Where(_ => false);
        return q.Where(System.Linq.Expressions.Expression.Lambda<Func<Employee, bool>>(body, pe));
    }
}

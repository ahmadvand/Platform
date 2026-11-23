using Platform.Web.Domain;
using Platform.Web.Services;
using Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Org;

// ======================================================================
// فاز ۶۹ — پلاگین دیتای ماژول سازمان و پرسنل
// ----------------------------------------------------------------------
// تا فاز ۶۸ جدول‌های پرسنلی «پایهٔ توکار» بودند (اسکیمایشان همیشه ساخته
// می‌شد). از این فاز مالکیت اسکیما/سید نمایشی/آبشار حذف شرکت/شمارش‌ها با
// همین IHrDataModule است — ماژول غایب = نه جدول، نه سید، نه DI.
// قرارداد IPeopleDirectory هم همین‌جا پیاده‌سازی می‌شود: هر جای هسته/ماژول
// دیگر که به پرسنل نیاز دارد، از قرارداد می‌خواند (org غایب = Null بی‌خطر).
// نوع‌ها در پلتفرم مشترک‌اند (Platform.Web.Domain) تا ماژول‌های سیستم
// بدون ارجاع به ماژول دیگر کامپایل شوند؛navigation بین‌خانواده‌ای حذف و
// FK بین‌خانواده‌ای مشروط در ComposeCrossModule اعمال می‌شود.
// ======================================================================
public sealed class OrgDataModule : IHrDataModule
{
    public string Code => "org";

    // فاز ۶۹ — خانوادهٔ نوع‌های این ماژول (مالکیت اسکیما) — داده‌محور برای گارد Ignore
    public Type[] FamilyTypes => [typeof(Department), typeof(Position), typeof(Employee),
        typeof(Contract), typeof(Decree), typeof(Holiday)];

    // فاز ۱۲۲ — اعلام خانواده‌های داده برای مرکز درون‌ریزی/برون‌ریزی (عنوان/گروه/کلید تجاری)
    public HrDataExchangeDecl[] DataExchange =>
    [
        new("Employee", "پرسنل", "منابع انسانی", true, "PersonnelCode"),
        new("Department", "واحدهای سازمانی", "منابع انسانی", true, "Code"),
        new("Position", "پست‌های سازمانی", "منابع انسانی", true, "Code"),
        new("Contract", "قراردادهای پرسنل", "منابع انسانی", false),
        new("Decree", "حکم‌های پرسنل", "منابع انسانی", false),
        new("Holiday", "تعطیلات رسمی", "حضور و غیاب", true, "Date"),
    ];
    public string Title => "سازمان و پرسنل";
    public string Icon => "🏢";
    public string Description => "چارت سازمانی، پرسنل، قراردادها و احکام، تعطیلات رسمی و ورود اکسل — پایهٔ مشترک همهٔ ماژول‌های سیستم";

    public void ConfigureDataServices(IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? useDb)
    {
        services.AddScoped<IPeopleDirectory, OrgPeopleDirectory>();
    }

    // ==================================================================
    // اسکیما — عیناً همان پیکربندی پایهٔ سابق (DDL بایت‌به‌بایت یکسان)
    // ==================================================================
    public void ComposeModel(ModelBuilder mb)
    {
        mb.Entity<Department>().ToTable("Departments");
        mb.Entity<Position>().ToTable("Positions");
        mb.Entity<Employee>().ToTable("Employees");
        mb.Entity<Contract>().ToTable("Contracts");
        mb.Entity<Decree>().ToTable("Decrees");
        mb.Entity<Holiday>().ToTable("Holidays");

        // ایندکس‌های یکتا — قرینهٔ مدل مرکب سابق
        mb.Entity<Department>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<Position>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        mb.Entity<Employee>().HasIndex(x => x.PersonnelCode).IsUnique();
        mb.Entity<Employee>().HasIndex(x => x.NationalId).IsUnique();
        mb.Entity<Contract>().HasIndex(x => x.ContractNo).IsUnique();
        mb.Entity<Holiday>().HasIndex(x => x.Date).IsUnique();

        // روابط درون‌خانواده‌ای + شرکت (core) — با همان رفتار حذف DDL موجود
        mb.Entity<Department>().HasOne(d => d.Company).WithMany() // nav-typed — جلوگیری از FK شادو CompanyId1 (فاز ۶۹)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Department>().HasOne(x => x.Parent).WithMany(d => d.Children)
            .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.ClientSetNull);
        mb.Entity<Position>().HasOne(p => p.Company).WithMany() // nav-typed — جلوگیری از FK شادو (فاز ۶۹)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.ClientSetNull);
        mb.Entity<Position>().HasOne(x => x.Department).WithMany(d => d.Positions)
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Employee>().HasOne(e => e.Company).WithMany() // nav-typed — جلوگیری از FK شادو (فاز ۶۹)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Employee>().HasOne(x => x.Department).WithMany()
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.ClientSetNull);
        mb.Entity<Employee>().HasOne(x => x.Position).WithMany()
            .HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.ClientSetNull);
        mb.Entity<Contract>().HasOne(x => x.Employee).WithMany(e => e.Contracts)
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Decree>().HasOne(x => x.Employee).WithMany(e => e.Decrees)
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);

        // طول ستون‌ها — قرینهٔ پیکربندی پایهٔ سابق
        mb.Entity<Department>().Property(x => x.Code).HasMaxLength(100);
        mb.Entity<Position>().Property(x => x.Code).HasMaxLength(100);
    }

    /// <summary>FKهای بین‌خانواده‌ای — گذر دوم (بعد از مدل همهٔ ماژول‌ها).
    /// کاربر ← پرسنل: هسته همیشه حاضر است؛ org غایب = این قلاب خودش را رد می‌کند.</summary>
    public void ComposeCrossModule(ModelBuilder mb)
    {
        if (mb.Model.FindEntityType(typeof(User)) is null) return;
        mb.Entity<User>().HasOne<Employee>().WithOne()
            .HasForeignKey<User>(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
    }

    // ==================================================================
    // سید نمایشی (پورت SeedAllAsync هاست — بخش سازمان)
    // ==================================================================

    /// <summary>PreSeed — واحدها/پست‌ها/پرسنل/قراردادها/احکام شرکت نمونه (پیش از سید ماژول‌های
    /// دیگر که به شناسهٔ پرسنل نیاز دارند). شیفت‌ها ∈ حضور (PreSeed — الفبایی قبل از org).</summary>
    public async Task PreSeedAsync(HrSeedContext ctx)
    {
        if (!ctx.DemoData) return;
        var db = ctx.Db;
        var company = await db.Set<Company>().FirstOrDefaultAsync(c => c.Id == ctx.DemoCompanyId)
                      ?? await db.Set<Company>().OrderBy(c => c.Code).FirstOrDefaultAsync();
        if (company is null) return; // بدون شرکت، دموی سازمانی معنا ندارد

        // ---------- دپارتمان‌ها (درخت) ----------
        Department MkDept(string name, string code, string? parentId = null)
        {
            var d = new Department { Name = name, Code = code, CompanyId = company.Id, ParentId = parentId };
            db.Set<Department>().Add(d);
            return d;
        }
        var dExec = MkDept("دفتر مدیریت عامل", "EXE");
        var dHR = MkDept("منابع انسانی", "HR");
        var dSec = MkDept("حراست و حفاظت", "SEC");
        var dProd = MkDept("تولید و بهره‌برداری", "PRD");
        var dFin = MkDept("مالی و اداری", "FIN");
        var dTech = MkDept("فنی و مهندسی", "ENG");
        var dComm = MkDept("بازرگانی و خرید", "COM");
        var dHSE = MkDept("HSE و ایمنی", "HSE");
        await db.SaveChangesAsync();
        var dOlef = MkDept("واحد الفین", "PRD-OLF", dProd.Id);
        var dPoly = MkDept("واحد پلی‌اتیلن", "PRD-PLE", dProd.Id);
        var dUtil = MkDept("واحد یوتیلیتی", "PRD-UTL", dProd.Id);
        await db.SaveChangesAsync();

        // ---------- پست‌های سازمانی ----------
        Position MkPos(string title, string code, Department dept, string? grade = null)
        {
            var p = new Position { Title = title, Code = code, DepartmentId = dept.Id, Grade = grade };
            db.Set<Position>().Add(p);
            return p;
        }
        var pCEO = MkPos("مدیرعامل", "P-CEO", dExec);
        var pHRM = MkPos("مدیر منابع انسانی", "P-HRM", dHR);
        var pHRS = MkPos("کارشناس منابع انسانی", "P-HRS", dHR);
        var pSecC = MkPos("رئیس حراست", "P-SEC", dSec);
        var pGuard = MkPos("نگهبان", "P-GRD", dSec);
        var pProdM = MkPos("مدیر تولید", "P-PRM", dProd);
        var pShiftL = MkPos("سرپرست شیفت", "P-SHL", dProd);
        var pOper = MkPos("اپراتور تولید", "P-OPR", dProd);
        var pFinM = MkPos("رئیس مالی", "P-FIN", dFin);
        var pAcc = MkPos("حسابدار", "P-ACC", dFin);
        var pEngM = MkPos("مدیر فنی و مهندسی", "P-ENG", dTech);
        var pTech = MkPos("تکنسین نگهداری و تعمیرات", "P-TEC", dTech);
        var pBuy = MkPos("کارشناس بازرگانی و خرید", "P-BUY", dComm);
        var pHse = MkPos("کارشناس HSE", "P-HSE", dHSE);
        await db.SaveChangesAsync();

        // ---------- پرسنل (شیفت‌ها از PreSeed حضور — org پس از attendance اجرا می‌شود) ----------
        var sh = new DemoShiftIds(
            ctx.ShiftIds.GetValueOrDefault(HrDemoShiftKeys.Admin),
            ctx.ShiftIds.GetValueOrDefault(HrDemoShiftKeys.Morning),
            ctx.ShiftIds.GetValueOrDefault(HrDemoShiftKeys.Evening),
            ctx.ShiftIds.GetValueOrDefault(HrDemoShiftKeys.Night),
            ctx.ShiftIds.GetValueOrDefault(HrDemoShiftKeys.H12),
            ctx.ShiftIds.GetValueOrDefault(HrDemoShiftKeys.Cycle));

        var specs = new (string First, string Last, Department Dept, Position Pos, string Type, string? ShiftId, decimal Salary, int HireYearsAgo, decimal? LeaveUsed)[]
        {
            ("کامران", "رستمی", dExec, pCEO, "رسمی", sh.Admin, 980_000_000m, 14, null),
            ("مریم", "تهرانی", dHR, pHRM, "رسمی", sh.Admin, 620_000_000m, 11, null),
            ("سعید", "قاسمی", dHR, pHRS, "قراردادی", sh.Admin, 340_000_000m, 4, null),
            ("پریسا", "علوی", dHR, pHRS, "قراردادی", sh.Admin, 315_000_000m, 2, null),
            ("حسین", "بهرامی", dSec, pSecC, "رسمی", sh.Admin, 540_000_000m, 12, null),
            ("جواد", "صادقی", dSec, pGuard, "پیمانکاری", sh.H12, 190_000_000m, 1, null),
            ("یاسر", "فتحی", dSec, pGuard, "پیمانکاری", sh.Night, 190_000_000m, 1, null),
            ("لیلا", "کاویانی", dSec, pGuard, "پیمانکاری", sh.Morning, 185_000_000m, 2, null),
            ("فرهاد", "سلطانی", dProd, pProdM, "رسمی", sh.Admin, 760_000_000m, 13, null),
            ("رضا", "کریمی", dOlef, pShiftL, "رسمی", sh.Morning, 480_000_000m, 9, null),
            ("مهدی", "نوری", dOlef, pShiftL, "رسمی", sh.Evening, 465_000_000m, 8, null),
            ("امیر", "محمدی", dOlef, pOper, "قراردادی", sh.Morning, 260_000_000m, 3, null),
            ("علی", "احمدی", dOlef, pOper, "قراردادی", sh.Evening, 255_000_000m, 3, null),
            ("حسن", "جعفری", dOlef, pOper, "قراردادی", sh.Night, 260_000_000m, 2, null),
            ("سامان", "امیری", dPoly, pOper, "اقماری", sh.Cycle, 385_000_000m, 5, null),
            ("هومن", "مرادی", dPoly, pOper, "اقماری", sh.Cycle, 380_000_000m, 5, null),
            ("بهزاد", "شریفی", dPoly, pOper, "قراردادی", sh.Morning, 250_000_000m, 2, null),
            ("آرش", "رستمی", dUtil, pOper, "قراردادی", sh.Evening, 240_000_000m, 2, null),
            ("مسعود", "اکبری", dFin, pFinM, "رسمی", sh.Admin, 640_000_000m, 10, null),
            ("زهرا", "حسینی", dFin, pAcc, "قراردادی", sh.Admin, 330_000_000m, 4, null),
            ("فاطمه", "موسوی", dFin, pAcc, "قراردادی", sh.Admin, 310_000_000m, 3, null),
            ("ناصر", "زارعی", dTech, pEngM, "رسمی", sh.Admin, 700_000_000m, 12, null),
            ("بهنام", "نیک‌نام", dTech, pTech, "قراردادی", sh.Admin, 295_000_000m, 3, null),
            ("کیان", "عبدی", dTech, pTech, "قراردادی", sh.Morning, 285_000_000m, 1, null),
            ("سارا", "موسوی", dComm, pBuy, "قراردادی", sh.Admin, 350_000_000m, 5, null),
            ("الناز", "شاهسوند", dComm, pBuy, "قراردادی", sh.Admin, 320_000_000m, 2, null),
            ("شیما", "موحدی", dHSE, pHse, "رسمی", sh.Admin, 420_000_000m, 7, null),
            ("نگار", "پرویزی", dHSE, pHse, "قراردادی", sh.Admin, 360_000_000m, 3, null),
        };

        var empIds = new List<(string Id, string DeptId, string? ShiftId, string Type, string PosCode)>();
        var empNames = new Dictionary<string, string>();
        int persNo = 1001;
        int empSeq = 0;
        foreach (var s in specs)
        {
            var idx = empSeq++;
            var natId = ctx.Rint(1_000_000_000, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var hire = DateTime.Now.AddDays(-365 * s.HireYearsAgo - ctx.Rint(0, 200));
            var emp = new Employee
            {
                PersonnelCode = (persNo++).ToString(System.Globalization.CultureInfo.InvariantCulture),
                FirstName = s.First, LastName = s.Last,
                NationalId = natId,
                BirthDate = $"13{ctx.Rint(40, 75)}/0{ctx.Rint(1, 9)}/{ctx.Rint(10, 28):00}",
                Phone = $"0912{ctx.Rint(1000000, 9999999)}",
                Email = $"emp{persNo - 1}@parsasia.ir",
                EmploymentType = s.Type, Status = "فعال",
                HireDate = hire, CompanyId = company.Id,
                DepartmentId = s.Dept.Id, PositionId = s.Pos.Id,
                BaseSalary = s.Salary, ShiftId = s.ShiftId,
                MaritalStatus = idx % 3 == 2 ? "مجرد" : "متأهل",
                ChildCount = idx % 3 == 2 ? 0 : (idx + ctx.Rint(0, 2)) % 3,
                AnnualLeaveEnt = s.Type == "رسمی" ? 26 : 22,
                AnnualLeaveUsed = s.LeaveUsed ?? ctx.Rint(2, 14),
            };
            db.Set<Employee>().Add(emp);
            await db.SaveChangesAsync();
            empIds.Add((emp.Id, s.Dept.Id, s.ShiftId, s.Type, s.Pos.Code));
            empNames[emp.Id] = $"{s.First} {s.Last}";
        }

        // ---------- قراردادها و احکام ----------
        var today = DateTime.Now.Date;
        for (int i = 0; i < empIds.Count; i++)
        {
            var e = empIds[i];
            var spec = specs[i];
            var start = today.AddDays(-365 * spec.HireYearsAgo);
            var cType = "دائم";
            DateTime? end = null;
            if (spec.Type == "قراردادی")
            {
                cType = "موقت";
                end = start.AddDays(365);
                if (end < today) end = today.AddDays(ctx.Rint(10, 300));
            }
            else if (spec.Type == "پیمانکاری")
            {
                cType = "پروژه‌ای";
                end = today.AddDays(ctx.Rint(-5, 120));
            }
            else if (spec.HireYearsAgo <= 1)
            {
                cType = "آزمایشی";
                end = start.AddDays(90);
            }
            db.Set<Contract>().Add(new Contract
            {
                EmployeeId = e.Id, Type = cType,
                ContractNo = $"CT-1404-{100 + i}",
                StartDate = start, EndDate = end, Salary = spec.Salary,
                Status = end is not null && end < today ? "منقضی" : "فعال",
            });
            db.Set<Decree>().Add(new Decree
            {
                EmployeeId = e.Id, Type = "استخدام",
                DecreeNo = $"DEC-{start.Year}-{500 + i}",
                Date = start, Content = "احکام استخدام اولیه صادر و ابلاغ گردید.",
                Acknowledged = ctx.Rand() > 0.2,
            });
            if (spec.HireYearsAgo >= 5 && ctx.Rand() > 0.4)
            {
                db.Set<Decree>().Add(new Decree
                {
                    EmployeeId = e.Id, Type = "ترفیع",
                    DecreeNo = $"DEC-1403-{700 + i}",
                    Date = today.AddDays(-ctx.Rint(60, 700)),
                    Content = "در اجرای ماده ۲۹ آیین‌نامه کارگزینی به رتبه بالاتر ارتقا یافت.",
                    Acknowledged = ctx.Rand() > 0.3,
                });
            }
        }
        await db.SaveChangesAsync();

        // سپردن شناسه‌ها به بستر سید — ماژول‌های دیگر (حضور/دسترسی/حقوق/دروازه) مصرف می‌کنند
        ctx.Employees =
            empIds.Select(e => new HrDemoEmployee(e.Id, e.DeptId, e.ShiftId, e.Type, e.PosCode)).ToList();
        ctx.EmployeeNames = empNames;
    }

    public Task SeedAsync(HrSeedContext ctx) => Task.CompletedTask;

    /// <summary>پاک‌سازی دادهٔ نمایشی سازمان (Reseed) — ترتیب فرزند ← والد.</summary>
    public async Task ClearDemoAsync(DbContext db)
    {
        db.Set<Decree>().RemoveRange(await db.Set<Decree>().ToListAsync());
        db.Set<Contract>().RemoveRange(await db.Set<Contract>().ToListAsync());
        db.Set<Employee>().RemoveRange(await db.Set<Employee>().ToListAsync());
        db.Set<Position>().RemoveRange(await db.Set<Position>().ToListAsync());
        db.Set<Department>().RemoveRange(await db.Set<Department>().ToListAsync());
        db.Set<Holiday>().RemoveRange(await db.Set<Holiday>().ToListAsync());
        await db.SaveChangesAsync();
    }

    /// <summary>آبشار حذف شرکت — دادهٔ سازمانی متعلق به شرکت داده‌شده (قرارداد فاز ۶۷).</summary>
    public async Task OnCompanyDeleteAsync(DbContext db, string companyId)
    {
        db.Set<Decree>().RemoveRange(await db.Set<Decree>()
            .Where(x => x.Employee != null && x.Employee.CompanyId == companyId).ToListAsync());
        db.Set<Contract>().RemoveRange(await db.Set<Contract>()
            .Where(x => x.Employee != null && x.Employee.CompanyId == companyId).ToListAsync());
        db.Set<Employee>().RemoveRange(await db.Set<Employee>().Where(x => x.CompanyId == companyId).ToListAsync());
        db.Set<Position>().RemoveRange(await db.Set<Position>().Where(x => x.CompanyId == companyId).ToListAsync());
        db.Set<Department>().RemoveRange(await db.Set<Department>().Where(x => x.CompanyId == companyId).ToListAsync());
        await db.SaveChangesAsync();
    }

    /// <summary>شمارش‌های صفحهٔ شرکت‌ها — kinds: departments/positions/employees.</summary>
    public Task<Dictionary<string, int>> CompanyCountsAsync(DbContext db, string kind) => kind switch
    {
        "departments" => db.Set<Department>().Where(d => d.CompanyId != null)
            .GroupBy(d => d.CompanyId!).ToDictionaryAsync(g => g.Key, g => g.Count()),
        "positions" => db.Set<Position>().Where(p => p.CompanyId != null)
            .GroupBy(p => p.CompanyId!).ToDictionaryAsync(g => g.Key, g => g.Count()),
        "employees" => db.Set<Employee>()
            .GroupBy(e => e.CompanyId).ToDictionaryAsync(g => g.Key, g => g.Count()),
        _ => Task.FromResult(new Dictionary<string, int>()),
    };

    public Task<int> CompanyCountAsync(DbContext db, string kind, string companyId) => kind switch
    {
        "departments" => db.Set<Department>().CountAsync(d => d.CompanyId == companyId),
        "positions" => db.Set<Position>().CountAsync(p => p.CompanyId == companyId),
        "employees" => db.Set<Employee>().CountAsync(e => e.CompanyId == companyId),
        _ => Task.FromResult(0),
    };
}

/// <summary>شناسهٔ شیفت‌های نمونه برای سید پرسنل (شکل داخلی — از کلیدهای قراردادی پر می‌شود).</summary>
public sealed record DemoShiftIds(string? Admin, string? Morning, string? Evening, string? Night, string? H12, string? Cycle);

// ======================================================================
// دایرکتوری پرسنل — پیاده‌سازی IPeopleDirectory روی کانتکست مرکب هاست
// (همان کوئری‌های قبلی کنترلرهای هسته — با شکل DTO سبک قراردادی)
// ======================================================================
public sealed class OrgPeopleDirectory(AppDbContext db, IAttendanceGateway att) : IPeopleDirectory
{
    public Task<List<HrPersonOption>> ActiveOptionsAsync() =>
        db.Set<Employee>().Where(e => e.Status == "فعال").OrderBy(e => e.PersonnelCode)
            .Select(e => new HrPersonOption(e.Id, e.PersonnelCode, e.FullName, e.CompanyId))
            .ToListAsync();

    public Task<List<HrDeptOption>> DepartmentsAsync() =>
        db.Set<Department>().OrderBy(d => d.Name)
            .Select(d => new HrDeptOption(d.Id, d.Name)).ToListAsync();

    public Task<List<HrPersonOption>> UnlinkedAsync() =>
        db.Set<Employee>().Where(e => !db.Users.Any(u => u.EmployeeId == e.Id))
            .OrderBy(e => e.PersonnelCode)
            .Select(e => new HrPersonOption(e.Id, e.PersonnelCode, e.FullName, e.CompanyId))
            .ToListAsync();

    public async Task<HrPersonBrief?> FindAsync(string employeeId)
    {
        var e = await db.Set<Employee>().FindAsync(employeeId);
        return e is null ? null : new HrPersonBrief(e.Id, e.PersonnelCode, e.FullName, e.CompanyId);
    }

    public Task<Dictionary<string, HrPersonBrief>> BriefsAsync(IReadOnlyCollection<string> employeeIds)
    {
        if (employeeIds.Count == 0) return Task.FromResult(new Dictionary<string, HrPersonBrief>());
        return db.Set<Employee>().Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => new HrPersonBrief(e.Id, e.PersonnelCode, e.FullName, e.CompanyId));
    }

    public async Task<string?> FindIdByNameAsync(string firstName, string lastName)
        => (await db.Set<Employee>().FirstOrDefaultAsync(e => e.FirstName == firstName && e.LastName == lastName))?.Id;

    public async Task<List<HrPersonExportRow>> ExportRowsAsync()
    {
        // نام شیفت ∈ ماژول حضور (قرارداد؛ غایب = ستون شیفت خالی) + تاریخ شمسی استخدام
        var shiftNames = await att.ShiftNamesAsync();
        var rows = await db.Set<Employee>().Include(e => e.Department).Include(e => e.Position)
            .OrderBy(e => e.PersonnelCode)
            .Select(e => new
            {
                e.PersonnelCode, e.FirstName, e.LastName, e.NationalId,
                Dept = e.Department != null ? e.Department.Name : "",
                Pos = e.Position != null ? e.Position.Title : "",
                e.EmploymentType, e.ShiftId, e.BaseSalary,
                Leave = e.AnnualLeaveEnt - e.AnnualLeaveUsed,
                e.Status, e.HireDate,
            })
            .ToListAsync();
        return rows.Select(e => new HrPersonExportRow(
            e.PersonnelCode, e.FirstName, e.LastName, e.NationalId, e.Dept, e.Pos,
            e.EmploymentType,
            e.ShiftId is not null && shiftNames.TryGetValue(e.ShiftId, out var shn) ? shn : "",
            e.BaseSalary, e.Leave, e.Status, Jalali.Fmt(e.HireDate))).ToList();
    }

    public async Task<HrSelfProfile?> SelfProfileAsync(string? personnelCode, string? linkedEmployeeId)
    {
        IQueryable<Employee> q = db.Set<Employee>();
        Employee? emp;
        if (!string.IsNullOrEmpty(personnelCode))
            emp = await q.FirstOrDefaultAsync(e => e.PersonnelCode == personnelCode && e.Status == "فعال");
        else if (!string.IsNullOrEmpty(linkedEmployeeId))
            emp = await q.FirstOrDefaultAsync(e => e.Id == linkedEmployeeId && e.Status == "فعال");
        else return null;
        if (emp is null) return null;
        return new HrSelfProfile(emp.Id, emp.PersonnelCode, emp.FullName,
            emp.Department != null ? emp.Department.Name : "", emp.Position != null ? emp.Position.Title : "",
            emp.EmploymentType, emp.AnnualLeaveEnt - emp.AnnualLeaveUsed, emp.SickLeaveEnt - emp.SickLeaveUsed);
    }

    public async Task<HrPersonAuth?> AuthenticateAsync(string personnelCode, string nationalId)
    {
        var e = await db.Set<Employee>().FirstOrDefaultAsync(x =>
            x.PersonnelCode == personnelCode && x.NationalId == nationalId && x.Status == "فعال");
        return e is null ? null : new HrPersonAuth(e.Id, e.PersonnelCode, e.FullName, e.CompanyId);
    }

    public Task<List<HrSearchHit>> SearchAsync(string q, int limit) =>
        db.Set<Employee>()
            .Where(e => e.FirstName.Contains(q) || e.LastName.Contains(q) || e.PersonnelCode.Contains(q))
            .OrderBy(e => e.PersonnelCode).Take(limit)
            .Select(e => new HrSearchHit(
                e.PersonnelCode + " — " + e.FirstName + " " + e.LastName,
                "/Employees?q=" + q))
            .ToListAsync();

    public Task<int> ActiveCountAsync() =>
        db.Set<Employee>().CountAsync(e => e.Status == "فعال");

    public Task<Dictionary<string, int>> EmploymentTypeCountsAsync() =>
        db.Set<Employee>().Where(e => e.Status == "فعال")
            .GroupBy(e => e.EmploymentType)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

    public Task<Dictionary<string, int>> CountsByCompanyAsync() =>
        db.Set<Employee>().GroupBy(e => e.CompanyId).ToDictionaryAsync(g => g.Key, g => g.Count());

    public Task<int> CountForCompanyAsync(string companyId) =>
        db.Set<Employee>().CountAsync(e => e.CompanyId == companyId);

    public async Task<List<HrContractExpiryRow>> ContractExpiryAsync(int days)
    {
        var until = DateTime.Now.Date.AddDays(days);
        var rows = await db.Set<Contract>()
            .Where(c => c.Status == "فعال" && c.EndDate != null && c.EndDate >= DateTime.Now.Date && c.EndDate <= until)
            .OrderBy(c => c.EndDate)
            .Select(c => new { c.Employee!.FirstName, c.Employee.LastName, c.Employee.PersonnelCode, c.EndDate, c.ContractNo })
            .ToListAsync();
        return rows.Select(x => new HrContractExpiryRow(x.FirstName, x.LastName, x.PersonnelCode, x.EndDate!.Value, x.ContractNo)).ToList();
    }
}

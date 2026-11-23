using Platform.Web.Data;
using HrModules.Access.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrModules.Access.Data;

// ======================================================================
// فاز ۶۲ — سیدِ داده‌های اولیهٔ ماژول کنترل تردد/دسترسی (IModuleSeeder)
// ----------------------------------------------------------------------
// مخزن کارت‌های شناسایی (فاز ۲۹) — خودترمیم و مستقل از سیدر دادهٔ کسب‌وکار:
// کارت‌های فعالِ هم‌راستا با کارت‌های ثبت‌شده روی دستگاه‌ها + کارت آزاد انبار + یک کارت مفقود.
// کارت‌ها دادهٔ ماژول Access؛ کارت‌سِمت دستگاه (DeviceUser) دادهٔ پایه (HrBaseDbContext) —
// بدون هرگونه ارجاع مستقیم به ماژول دیگر.
// ======================================================================
public sealed class AccessSeeder : IModuleSeeder
{
    public string ModuleCode => "access";
    public int Order => 20;

    public async Task SeedAsync(IServiceProvider scopedServices, bool demoData, CancellationToken ct = default)
    {
        var db = scopedServices.GetRequiredService<AccessDbContext>();
        if (await db.Cards.AnyAsync(ct)) return;

        // فاز ۶۹ — DeviceUser (خانوادهٔ devices) فقط در مدل «مرکب» است؛ غایب = سید کارت رد می‌شود
        var cmpDb = scopedServices.GetServices<DbContext>()
            .FirstOrDefault(d => d.Model.FindEntityType(typeof(Platform.Web.Domain.DeviceUser)) is not null);
        if (cmpDb is null) return;
        var duCards = await cmpDb.Set<DeviceUser>()
            .Where(du => du.CardNumber != null && du.EmployeeId != null)
            .OrderBy(du => du.Pin)
            .ToListAsync(ct);
        var allEmps = await db.Employees.OrderBy(e => e.PersonnelCode).ToListAsync(ct);
        var seenCards = new HashSet<string>();
        int cardIdx = 0;
        foreach (var du in duCards)
        {
            if (du.CardNumber is null || !seenCards.Add(du.CardNumber)) continue;
            var emp = allEmps.FirstOrDefault(e => e.Id == du.EmployeeId);
            if (emp is null) continue;
            var temp = cardIdx == 2; // سومی موقت با انقضا
            db.Cards.Add(new Card
            {
                CardNo = du.CardNumber,
                EmployeeId = emp.Id,
                Type = cardIdx % 3 == 0 ? "Mifare" : cardIdx % 3 == 1 ? "UHF" : "RFID",
                Status = temp ? "موقت" : "فعال",
                IssuedAt = DateTime.Now.AddDays(-Random.Shared.Next(60, 500)),
                ExpiresAt = temp ? DateTime.Now.AddDays(45) : null,
                Note = temp ? "کارت موقت پیمانکار" : null,
            });
            cardIdx++;
        }
        // کارت‌های آزاد انبار + یک کارت مفقود
        db.Cards.Add(new Card { CardNo = "00173800", Type = "Mifare", Status = "آزاد", Note = "انبار مرکزی" });
        db.Cards.Add(new Card { CardNo = "00173801", Type = "UHF", Status = "آزاد" });
        var lostHolder = allEmps.Skip(1).FirstOrDefault();
        if (lostHolder is not null)
            db.Cards.Add(new Card
            {
                CardNo = "00997700", Type = "RFID", Status = "گمشده", EmployeeId = lostHolder.Id,
                IssuedAt = DateTime.Now.AddDays(-300),
                Note = "به‌عنوان مفقود اعلام شد — اسکن رد می‌شود",
            });
        await db.SaveChangesAsync(ct);
    }
}

// فاز ۶۹ — ماژول تجهیزات و گیت‌وی (پلاگین): کنترلر پایهٔ هاست (MvcBase) و قراردادها.
// هیچ ارجاعی به ماژول دیگری — وابستگی فقط به پلتفرم/قراردادها/هاست (یک‌طرفه).
global using Platform.Web.Controllers;   // MvcBase
// AppDbContext/کانتکست مرکب هاست — لایهٔ دیتای ماژول از همان مسیر می‌خواند
global using Platform.Web.Data;   // MvcBase
global using Platform.Contracts;
// معادل usingهای ضمنی SDK وب (پروژهٔ پلاگین Razor SDK است — این usingها را نمی‌آورد)
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.DependencyInjection;

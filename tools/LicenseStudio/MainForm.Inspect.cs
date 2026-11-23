using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace LicenseStudio;

/// <summary>تب‌های بررسی مجوز، تاریخچه و راهنما — partial دوم MainForm.</summary>
public partial class MainForm
{
    // ------------------------------------------------------------------
    // تب ۲ — بررسی/خواندن مجوز
    // ------------------------------------------------------------------
    private TabPage BuildInspectTab()
    {
        var page = new TabPage("بررسی مجوز") { Padding = new Padding(12) };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // دکمه‌ها
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // کلید عمومی بررسی (فاز ۷۸)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // متن مجوز
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // نتیجه
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // جدول

        // دکمه‌ها — فاز ۵۶: هم‌سبک با بقیهٔ فرم (برند/ملایم + فونت وزیر)
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 0, 0, 8) };
        var btnOpen = new Button { Text = "بازکردن فایل مجوز…", Size = new Size(160, 34), BackColor = ColorTranslator.FromHtml("#eef7f6"), FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f) };
        var btnVerify = new Button { Text = "بررسی امضا و محتوا", Size = new Size(170, 34), BackColor = ColorTranslator.FromHtml("#0f766e"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f, FontStyle.Bold) };
        var lblRef = new Label { Text = "اثر انگشت سرور (اختیاری — برای تطبیق):", AutoSize = true, Padding = new Padding(14, 8, 6, 0) };
        _txtRefFp = new TextBox { Width = 300, Font = new Font("Consolas", 10f), RightToLeft = RightToLeft.No };
        btnOpen.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Title = "انتخاب فایل مجوز", Filter = "فایل مجوز (*.dat;*.txt)|*.dat;*.txt|همهٔ فایل‌ها (*.*)|*.*" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _txtContent.Text = File.ReadAllText(dlg.FileName);
            DoInspect();
        };
        btnVerify.Click += (_, _) => DoInspect();
        top.Controls.AddRange(new Control[] { btnOpen, btnVerify, lblRef, _txtRefFp });

        // کلید عمومی بررسی (فاز ۷۸) — رفع «امضای دیجیتال تأیید نشد» برای مجوزهایی که با کلید
        // دیگری (مثلاً جفت‌کلید ساخته‌شده در صفحهٔ مجوز سرور مشتری) امضا شده‌اند:
        var pubRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 0, 0, 8) };
        var btnPickPub = new Button { Text = "انتخاب کلید عمومی (public.pem / SPKI)…", AutoSize = true, MinimumSize = new Size(0, 32), BackColor = ColorTranslator.FromHtml("#eef7f6"), FlatStyle = FlatStyle.Flat, Font = Program.Vazir(9.5f) };
        _btnResetPub = new Button { Text = "بازگشت به کلید سازنده", AutoSize = true, MinimumSize = new Size(0, 32), Enabled = false, FlatStyle = FlatStyle.Flat, Font = Program.Vazir(9.5f) };
        _lblPubState = new Label { Text = "بررسی با کلید عمومی جاسازی‌شدهٔ سازنده", AutoSize = true, Padding = new Padding(10, 7, 0, 0), ForeColor = Color.DimGray, Font = Program.Vazir(9.5f) };
        btnPickPub.Click += (_, _) => PickPublicKey();
        _btnResetPub.Click += (_, _) => { _customPubKey = null; UpdatePubState(); };
        pubRow.Controls.AddRange(new Control[] { btnPickPub, _btnResetPub, _lblPubState });

        // محتوای مجوز
        _txtContent = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9.5f), RightToLeft = RightToLeft.No,
            PlaceholderText = "محتوای فایل مجوز را اینجا بچسبانید یا فایل را باز کنید…",
        };

        // نتیجه
        _lblInspectVerdict = new Label { Text = "", AutoSize = true, Font = Program.Vazir(11f, FontStyle.Bold), Padding = new Padding(0, 8, 0, 4) };

        // جدول جزئیات — فونت وزیر صریح (سرستون‌ها هم ارث می‌برند)
        _lvInspect = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false,
            Font = Program.Vazir(9.5f),
        };
        _lvInspect.Columns.Add("مورد", 210);
        _lvInspect.Columns.Add("مقدار", 640);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(pubRow, 0, 1);
        root.Controls.Add(_txtContent, 0, 2);
        root.Controls.Add(_lblInspectVerdict, 0, 3);
        root.Controls.Add(_lvInspect, 0, 4);
        page.Controls.Add(root);
        return page;
    }

    private void DoInspect()
    {
        _lblInspectVerdict.Text = "";
        _lvInspect.Items.Clear();
        // فاز ۷۸ — کلید عمومی فعال: کلید دلخواه کاربر (اگر انتخاب شده باشد) وگرنه کلید جاسازی‌شدهٔ سازنده
        var verifyKey = _customPubKey ?? EmbeddedKeys.PublicKeySpki;
        var res = LicenseInspector.Inspect(_txtContent.Text, verifyKey,
            string.IsNullOrWhiteSpace(_txtRefFp.Text) ? null : _txtRefFp.Text);

        if (!res.SignatureOk)
        {
            _lblInspectVerdict.Text = "✗ نامعتبر — " + res.Error;
            if (_customPubKey is null && res.Error!.Contains("امضا"))
                _lblInspectVerdict.Text += "\n💡 اگر این مجوز با کلید دیگری (مثلاً جفت‌کلید صفحهٔ مجوز سرور مشتری) امضا شده، کلید عمومی همان کلید را با دکمهٔ «انتخاب کلید عمومی» بدهید و دوباره بررسی کنید.";
            _lblInspectVerdict.ForeColor = Color.Firebrick;
            return;
        }
        if (res.Error is not null)
        {
            _lblInspectVerdict.Text = "⚠ " + res.Error;
            _lblInspectVerdict.ForeColor = Color.DarkGoldenrod;
        }
        else if (res.Expired == true)
        {
            _lblInspectVerdict.Text = "✗ این مجوز منقضی شده است";
            _lblInspectVerdict.ForeColor = Color.Firebrick;
        }
        else if (res.MachineMatch == false)
        {
            _lblInspectVerdict.Text = "⚠ امضا معتبر است اما برای سرور دیگری صادر شده";
            _lblInspectVerdict.ForeColor = Color.DarkGoldenrod;
        }
        else
        {
            _lblInspectVerdict.Text = "✓ امضای مجوز معتبر است";
            _lblInspectVerdict.ForeColor = Color.SeaGreen;
        }

        var p = res.Payload!;
        AddRow("محصول", p.Product + (res.ProductOk ? "" : "  (با محصول جاری مطابقت ندارد!)"));
        AddRow("دارندهٔ مجوز", p.Licensee);
        AddRow("اثر انگشت سرور", p.Machine + (res.MachineMatch is null ? "" : res.MachineMatch == true ? "  ✓ با مرجع مطابق است" : "  ✗ با مرجع مطابق نیست"));
        AddRow("تاریخ صدور", JalaliDates.FmtLong(p.IssuedAtUtc));
        AddRow("تاریخ انقضا", p.ExpiresAtUtc is { } exp ? JalaliDates.FmtLong(exp) + (res.Expired == true ? "  (منقضی)" : "") : "دائمی");
        AddRow("سقف پرسنل", p.MaxEmployees is int m ? Fingerprint.ToPersianDigits(m.ToString()) + " نفر" : "بدون محدودیت");
        AddRow("ماژول‌های مجاز", LicenseModules.Display(p.Modules));
        AddRow("حالت اجرای قفل", LicenseLockMode.Display(p.LockMode) + (p.LockMode is null ? "" : $"  ({p.LockMode})"));
        AddRow("یادداشت", p.Notes ?? "—");
    }

    // ------------------------------------------------------------------
    // فاز ۷۸ — کلید عمومی دلخواه برای بررسی مجوز
    // ------------------------------------------------------------------
    private void PickPublicKey()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "انتخاب کلید عمومی امضای مجوز",
            Filter = "کلید عمومی (*.pem;*.key;*.cer;*.txt)|*.pem;*.key;*.cer;*.txt|همهٔ فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var content = File.ReadAllText(dlg.FileName);
            var fp = PublicKeyFingerprint(content); // اعتبارسنجی + اثر انگشت — نامعتبر = استثنا
            _customPubKey = content;
            UpdatePubState(dlg.FileName, fp);
            DoInspect(); // بلافاصله با کلید جدید بررسی مجدد
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "این فایل کلید عمومی معتبر نیست:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, RtlMsg);
        }
    }

    private void UpdatePubState(string? path = null, string? fp = null)
    {
        if (_customPubKey is null)
        {
            _lblPubState.Text = "بررسی با کلید عمومی جاسازی‌شدهٔ سازنده";
            _lblPubState.ForeColor = Color.DimGray;
            _btnResetPub.Enabled = false;
            return;
        }
        var name = path is null ? "کلید سفارشی" : Path.GetFileName(path);
        _lblPubState.Text = $"✓ بررسی با کلید عمومی سفارشی: {name} — اثر انگشت کلید: {fp}";
        _lblPubState.ForeColor = Color.SeaGreen;
        _btnResetPub.Enabled = true;
    }

    /// <summary>اعتبارسنجی کلید عمومی (PEM یا SPKI/Base64) و محاسبهٔ اثر انگشت کوتاه SHA-256.</summary>
    private static string PublicKeyFingerprint(string keyContent)
    {
        using var rsa = RSA.Create();
        var trimmed = keyContent.Trim();
        if (trimmed.StartsWith("-----BEGIN", StringComparison.Ordinal))
            rsa.ImportFromPem(trimmed);
        else
            rsa.ImportSubjectPublicKeyInfo(
                Convert.FromBase64String(new string(trimmed.Where(c => !char.IsWhiteSpace(c)).ToArray())), out _);
        return Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))[..16];
    }

    private void AddRow(string k, string v)
    {
        var it = new ListViewItem(k);
        it.SubItems.Add(v);
        _lvInspect.Items.Add(it);
    }

    // ------------------------------------------------------------------
    // تب ۳ — تاریخچهٔ صدور
    // ------------------------------------------------------------------
    private TabPage BuildHistoryTab()
    {
        var page = new TabPage("تاریخچهٔ صدور") { Padding = new Padding(12) };
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 0, 0, 8) };
        var btnRefresh = new Button { Text = "بازخوانی", Size = new Size(110, 34), BackColor = ColorTranslator.FromHtml("#eef7f6"), FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f) };
        var btnOpenLog = new Button { Text = "بازکردن پوشهٔ گزارش‌ها", Size = new Size(180, 34), BackColor = ColorTranslator.FromHtml("#eef7f6"), FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f) };
        var lblHint = new Label
        {
            Text = "هر مجوز صادرشده خودکار در «تاریخچه» ثبت می‌شود؛ دوبار کلیک روی هر سطر، متن مجوز را برای ارسال به مشتری کپی می‌کند.",
            AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(12, 8, 0, 0),
        };
        btnRefresh.Click += (_, _) => RefreshHistory();
        btnOpenLog.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = AppDir, UseShellExecute = true }); } catch { }
        };
        top.Controls.AddRange(new Control[] { btnRefresh, btnOpenLog, lblHint });

        _lvHistory = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, Font = Program.Vazir(9.5f) };
        _lvHistory.Columns.Add("تاریخ صدور", 130);
        _lvHistory.Columns.Add("دارنده", 200);
        _lvHistory.Columns.Add("اثر انگشت سرور", 190);
        _lvHistory.Columns.Add("انقضا", 130);
        _lvHistory.Columns.Add("سقف پرسنل", 85);
        _lvHistory.Columns.Add("حالت قفل", 95);
        _lvHistory.Columns.Add("ماژول‌ها", 190);
        _lvHistory.Columns.Add("یادداشت", 220);
        _lvHistory.DoubleClick += (_, _) =>
        {
            if (_lvHistory.SelectedItems.Count == 0) return;
            if (_lvHistory.SelectedItems[0].Tag is LogEntry e && !string.IsNullOrEmpty(e.Content))
            {
                try { Clipboard.SetText(e.Content); }
                catch { }
                MessageBox.Show(this, "متن مجوز کپی شد — می‌توانید برای مشتری ارسال کنید", "کپی شد", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, RtlMsg);
            }
        };

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(_lvHistory, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    // ------------------------------------------------------------------
    // تب ۴ — راهنما
    // ------------------------------------------------------------------
    private TabPage BuildHelpTab()
    {
        var page = new TabPage("راهنما") { Padding = new Padding(12) };
        var box = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White, Font = Program.Vazir(10.5f),
            // فاز ۸۳ — خوانایی: راست‌به‌چپ صریح + شکست خط + بدون تب‌سنگین
            RightToLeft = RightToLeft.Yes, WordWrap = true,
            // فاز ۸۴ — رفع «راهنما یک دیوار متن پیوسته است»: سورس روی لینوکس با LF ذخیره شده و
            // کنترل EDIT ویندوز فقط CRLF را شکست خط می‌شناسد؛ نرمال‌سازی صریح پایان‌خط‌ها
            Text = HelpText.Replace("\r\n", "\n").Replace("\n", "\r\n"),
        };
        page.Controls.Add(box);
        return page;
    }

    // فاز ۸۳ — بازنویسی برای خوانایی انسانی: بخش‌بندی واضح با جداکننده، خطوط کوتاه، فهرست شماره‌دار
    private static string HelpText => """
        راهنمای صدور مجوز — سه گام ساده

        هدف: برای هر سرور مشتری یک فایل مجوز (.dat) بسازید که
        فقط روی همان سرور فعال شود.

        ----------------------------------------------
        گام ۱ — دریافت فایل درخواست از مشتری
        ----------------------------------------------
        ۱. مشتری با حساب «مدیر ارشد» وارد سامانه می‌شود، به صفحهٔ
           «مجوز نصب» (مسیر /License) می‌رود و دکمهٔ
           «دانلود فایل درخواست (.hrreq)» را می‌زند.
        ۲. فایل .hrreq را برای شما می‌فرستد. داخل آن امضای
           سخت‌افزاری همان سرور است (MachineGuid ویندوز یا
           machine-id لینوکس) — به همین دلیل مجوز ساخته‌شده
           فقط روی همان سرور معتبر است.

        ----------------------------------------------
        گام ۲ — صدور مجوز در همین برنامه
        ----------------------------------------------
        ۰) کلید خصوصی ندارید؟ «🔑 ساخت جفت‌کلید جدید…» را بزنید:
           private.pem نزد خودتان بماند؛ کلید عمومی را در فایل
           LicenseService.cs جای‌گذاری و برنامهٔ وب را دوباره بیلد کنید.
        ۱) در تب «صدور مجوز» کلید خصوصی را یک‌بار انتخاب کنید.
           مسیر به‌خاطر سپرده می‌شود؛ تا انتخاب نشود هشدار قرمز
           بالای فرم دیده می‌شود و پس از انتخاب سبز می‌شود.
        ۲) «بازکردن فایل درخواست مشتری (.hrreq)» را بزنید — امضا و
           نام شرکت خودکار پر می‌شوند. فایل ندارید؟ «چسباندن امضا»
           یا تایپ دستی — فاصله‌ها و ارقام فارسی خودکار اصلاح می‌شوند.
        ۳) «مدت اعتبار» را انتخاب کنید: دائمی / ۱ تا ۹ ماه /
           ۱ تا ۵ سال / سفارشی (در سفارشی، تاریخ شمسی آزاد است).
        ۴) «سقف پرسنل»: بدون محدودیت / ۱۰ تا ۵۰۰۰ / سفارشی.
        ۵) «ماژول‌های مجاز» را تیک بزنید — ماژول‌های تیک‌خورده داخل
           خود فایل مجوز درج می‌شوند و روی سرور مشتری فقط همان‌ها
           فعال می‌مانند؛ بقیه در پنل ماژول‌ها «قفل‌شده با مجوز»
           دیده می‌شوند.
           دکمه‌های «انتخاب همه» و «انتخاب هیچ» هم زیر فهرست است:
           «هیچ» یعنی همهٔ ماژول‌ها مجاز (سازگار با مجوزهای قدیمی).
        ۶) «حالت اجرای قفل» — داخل فایل مجوز درج می‌شود و بر
           تنظیمات appsettings سرور مشتری اولویت دارد:
           • پیش‌فرض سرور — طبق تنظیمات خود سرور مشتری (پیشنهادی)
           • فقط هشدار (warn) — در انقضا فقط صفحهٔ هشدار دیده می‌شود
           • قفل سخت (lock) — صفحه‌ها تا رفع مجوز بسته می‌مانند
             (API و پل دستگاه‌ها در هر دو حالت برای سلامت داده‌ها باز می‌مانند)
        ۷) «صدور مجوز» ← «ذخیرهٔ فایل .dat…» یا «کپی متن» ←
           فایل را برای مشتری بفرستید.

        ----------------------------------------------
        گام ۳ — فعال‌سازی روی سرور مشتری
        ----------------------------------------------
        مدیر مشتری در همان صفحهٔ «مجوز نصب»، فایل .dat را آپلود
        یا متن آن را می‌چسباند. سامانه امضا را با کلید عمومی
        جاسازی‌شده بررسی می‌کند؛ با تطبیق امضای سرور، مجوز فعال
        می‌شود.

        ----------------------------------------------
        نکته‌های امنیتی
        ----------------------------------------------
        ▪ private.pem فقط نزد شما بماند — هرگز با مشتری یا داخل
          بستهٔ محصول منتشر نشود. بدون این کلید ساختن مجوز معتبر
          عملاً ناممکن است (RSA-2048/SHA256).
        ▪ کپی فایل مجوز روی سرور دیگر کار نمی‌کند — امضای هر سرور
          یکتاست (خطای WrongMachine).
        ▪ دستکاری محتوای مجوز (سقف پرسنل/تاریخ) امضا را خراب
          می‌کند و مجوز رد می‌شود.
        ▪ چرخش کلید: کلید جدید بسازید، کلید عمومی جدید را در
          LicenseService.cs (ثابت PublicKeySpki) جای‌گذاری کنید،
          برنامهٔ سرور را به‌روز کنید و برای همهٔ مشتری‌ها مجوز جدید
          صادر کنید. «کپی کلید عمومی برای برنامهٔ وب» همین کار را
          آسان می‌کند.

        ----------------------------------------------
        قفل‌گذاری پس از فعال‌سازی (سمت سرور مشتری)
        ----------------------------------------------
        در appsettings.json سرور مشتری:
            "License": { "Mode": "Strict", "TrialDays": 30 }
        ▪ انقضای مجوز سامانه را از کار نمی‌اندازد: هیچ مسیری بسته
          نمی‌شود، دستگاه‌ها و API فعال می‌مانند؛ فقط پس از هر ورود
          صفحهٔ هشدار + بنر قرمز بالای صفحه‌ها تا ورود مجوز معتبر
          دیده می‌شود.
        ▪ حالت پیش‌فرض Off است. دورهٔ آزمایشی ۳۰ روزه به خود سرور
          گره خورده و دستکاری‌شدنش کشف می‌شود.
        ▪ اگر هنگام صدور، «حالت اجرای قفل» صریح انتخاب شده باشد
          (warn/lock)، همان بر تنظیمات سرور مشتری اولویت دارد و
          در «قفل سخت» دکمهٔ «ادامه به سامانه» کار نمی‌کند.

        ----------------------------------------------
        بررسی مجوز امضاشده با کلید دیگر
        ----------------------------------------------
        تب «بررسی مجوز» پیش‌فرض با کلید عمومی جاسازی‌شدهٔ سازنده
        بررسی می‌کند. مجوزی با کلید دیگری امضا شده باشد (مثلاً
        کلید ساخته‌شده در صفحهٔ مجوز سرور مشتری) پیام
        «امضای دیجیتال تأیید نشد» می‌بینید — کافی است کلید عمومی
        همان کلید (public.pem یا رشتهٔ SPKI/Base64) را با دکمهٔ
        «انتخاب کلید عمومی» بدهید تا تأیید شود؛ دکمهٔ «بازگشت به
        کلید سازنده» هم موجود است.

        ----------------------------------------------
        فایل‌های این برنامه
        ----------------------------------------------
        ▪ پوشهٔ داده‌ها (گزارش صدور و پیکربندی): پوشهٔ کاربر ←
          LicenseStudio
        ▪ تاریخچهٔ صدور: issued-log.jsonl — دوبار کلیک روی هر سطر
          = کپی متن مجوز
        ▪ فونت برنامه: وزیرمتن (جاسازی‌شده — بدون نیاز به نصب)
        """;
}

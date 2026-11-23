using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace HrLicenseStudio;

/// <summary>تب‌های بررسی مجوز، تاریخچه و راهنما — partial دوم MainForm.</summary>
public partial class MainForm
{
    // ------------------------------------------------------------------
    // تب ۲ — بررسی/خواندن مجوز
    // ------------------------------------------------------------------
    private TabPage BuildInspectTab()
    {
        // فاز ۷۲ب — تب اسکرول‌پذیر: در پنجرهٔ کوچک، نوار اسکرول ظاهر می‌شود و چیزی بریده نمی‌شود
        var page = new TabPage("بررسی مجوز") { Padding = new Padding(12), AutoScroll = true };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        root.MinimumSize = new Size(920, 560);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // دکمه‌ها
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // متن مجوز
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // نتیجه
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // جدول

        // دکمه‌ها — فاز ۵۶: هم‌سبک با بقیهٔ فرم (برند/ملایم + فونت وزیر)
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 0, 0, 8) };
        var btnOpen = new Button { Text = "بازکردن فایل مجوز…", Size = new Size(160, 34), BackColor = ColorTranslator.FromHtml("#eef7f6"), FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f) };
        var btnVerify = new Button { Text = "بررسی امضا و محتوا", Size = new Size(170, 34), BackColor = ColorTranslator.FromHtml("#0f766e"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f, FontStyle.Bold) };
        // فاز ۷۹ — افزودن کلید عمومی مشتری/نصب به فهرست کلیدهای پذیرفتهٔ بررسی
        var btnAddPub = new Button { Text = "افزودن کلید عمومی (public.pem)…", Size = new Size(215, 34), BackColor = ColorTranslator.FromHtml("#eef7f6"), FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f) };
        btnAddPub.Click += (_, _) => AddKnownPublicKey();
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
        top.Controls.AddRange(new Control[] { btnOpen, btnVerify, btnAddPub, lblRef, _txtRefFp });

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
        root.Controls.Add(_txtContent, 0, 1);
        root.Controls.Add(_lblInspectVerdict, 0, 2);
        root.Controls.Add(_lvInspect, 0, 3);
        page.Controls.Add(root);
        return page;
    }

    private void DoInspect()
    {
        _lblInspectVerdict.Text = "";
        _lvInspect.Items.Clear();

        // فاز ۷۸ — بررسی امضا با همهٔ کلیدهای شناخته‌شده به ترتیب:
        //   ۱) کلید رسمی محصول (جاسازی‌شده)  ۲) نیمهٔ عمومی کلید بارگذاری‌شده (private.pem فعلی)
        //   ۳) public.pem کنار فایل اجرایی / پوشهٔ داده‌ها (خروجی «ساخت جفت‌کلید جدید»)
        // قبلاً فقط مورد ۱ بررسی می‌شد؛ مجوزِ امضاشده با کلید محلی (مثل private.pem دانلودشده از
        // صفحهٔ مجوز نصب سرور) به‌اشتباه «نا معتبر» گزارش می‌شد — درخواست کاربر.
        var keys = new List<(string Label, string Key)>
        {
            ("کلید رسمی محصول (جاسازی‌شده)", EmbeddedKeys.PublicKeySpki),
        };
        if (!string.IsNullOrEmpty(_loadedPublicSpki) &&
            !string.Equals(_loadedPublicSpki, EmbeddedKeys.PublicKeySpki, StringComparison.Ordinal))
            keys.Add(("نیمهٔ عمومی کلید بارگذاری‌شدهٔ استودیو", _loadedPublicSpki!));
        // فاز ۷۹ — همهٔ public*.pemهای پوشهٔ داده (افزوده‌شده با «افزودن کلید عمومی…») + کنار فایل اجرایی
        var pubFiles = new List<string> { Path.Combine(AppContext.BaseDirectory, "public.pem"), Path.Combine(AppDir, "public.pem") };
        try
        {
            foreach (var f in Directory.GetFiles(AppDir, "public*.pem"))
                if (!pubFiles.Contains(f)) pubFiles.Add(f);
        }
        catch { }
        foreach (var pub in pubFiles)
        {
            try
            {
                if (File.Exists(pub) &&
                    !keys.Any(k => string.Equals(k.Key.Trim(), File.ReadAllText(pub).Trim(), StringComparison.Ordinal)))
                    keys.Add(("کلید عمومی کنار برنامه (" + Path.GetFileName(Path.GetDirectoryName(pub)) + ")", File.ReadAllText(pub)));
            }
            catch { }
        }

        var res = LicenseInspector.Inspect(_txtContent.Text, keys.ToArray(),
            string.IsNullOrWhiteSpace(_txtRefFp.Text) ? null : _txtRefFp.Text);

        if (!res.SignatureOk)
        {
            _lblInspectVerdict.Text = "✗ نامعتبر — " + res.Error;
            _lblInspectVerdict.ForeColor = Color.Firebrick;
            return;
        }
        var localKey = !string.IsNullOrEmpty(res.VerifiedWith) && res.VerifiedWith != "کلید رسمی محصول (جاسازی‌شده)";
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
            _lblInspectVerdict.Text = localKey
                ? "✓ امضای مجوز معتبر است (با کلید محلی این استودیو تأیید شد)"
                : "✓ امضای مجوز معتبر است";
            _lblInspectVerdict.ForeColor = Color.SeaGreen;
        }

        var p = res.Payload!;
        AddRow("محصول", p.Product + (res.ProductOk ? "" : "  (با محصول جاری مطابقت ندارد!)"));
        AddRow("دارندهٔ مجوز", p.Licensee);
        AddRow("اثر انگشت سرور", p.Machine + (res.MachineMatch is null ? "" : res.MachineMatch == true ? "  ✓ با مرجع مطابق است" : "  ✗ با مرجع مطابق نیست"));
        AddRow("تاریخ صدور", JalaliDates.FmtLong(p.IssuedAtUtc));
        AddRow("تاریخ انقضا", p.ExpiresAtUtc is { } exp ? JalaliDates.FmtLong(exp) + (res.Expired == true ? "  (منقضی)" : "") : "دائمی");
        AddRow("سقف پرسنل", p.MaxEmployees is int m ? Fingerprint.ToPersianDigits(m.ToString()) + " نفر" : "بدون محدودیت");
        AddRow("ماژول‌های مجاز", LicenseModules.Describe(p.Modules) + (p.Modules is { Length: > 0 }
            ? "  — بقیهٔ ماژول‌ها در سرور از منو حذف و بسته می‌شوند" : ""));
        AddRow("یادداشت", p.Notes ?? "—");
        // فاز ۷۸ — کدام کلید امضا را تأیید کرد + تذکر پذیرش سرور برای کلید محلی
        AddRow("تأیید امضا با", res.VerifiedWith ?? "—");
        if (localKey)
            AddRow("پذیرش در سرور", "این مجوز فقط روی سروری پذیرفته می‌شود که کلید عمومی همین کلید در آن ثبت باشد " +
                "(در سرور: صفحهٔ «مجوز نصب» ← «ساخت جفت‌کلید جدید» و بارگذاری این private.pem، یا جاسازی کلید عمومی در PublicKeySpki و انتشار نسخهٔ جدید)");
    }

    private void AddRow(string k, string v)
    {
        var it = new ListViewItem(k);
        it.SubItems.Add(v);
        _lvInspect.Items.Add(it);
    }

    /// <summary>
    /// فاز ۷۹ — افزودن دستی کلید عمومی (public.pem نصب مشتری) به فهرست پذیرش بررسی.
    /// فایل در پوشهٔ دادهٔ استودیو با نام public-<اثر>.pem ذخیره می‌شود؛ DoInspect همهٔ
    /// public*.pemهای همین پوشه را در کاندیدهای تأیید امضا می‌آورد.
    /// </summary>
    private void AddKnownPublicKey()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "انتخاب فایل کلید عمومی (public.pem)",
            Filter = "کلید PEM (*.pem;*.pub;*.txt)|*.pem;*.pub;*.txt|همهٔ فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var text = File.ReadAllText(dlg.FileName).Trim();
            using var rsa = System.Security.Cryptography.RSA.Create();
            if (text.StartsWith("-----BEGIN", StringComparison.Ordinal)) rsa.ImportFromPem(text);
            else rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray())), out _);
            var spki = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            var fpr = FingerprintOfSpki(spki);
            var dest = Path.Combine(AppDir, "public-" + fpr.Replace("-", "")[..8].ToLowerInvariant() + ".pem");
            File.WriteAllText(dest, text);
            MessageBox.Show(this, "کلید عمومی به فهرست بررسی افزوده شد:\n" + dest +
                "\nاز این پس امضاهای این کلید در «بررسی مجوز» تأیید می‌شوند.\nاثر انگشت: " + fpr,
                "کلید افزوده شد", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "این فایل کلید عمومی معتبر نیست:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>اثر انگشت کوتاه کلید عمومی (SHA-256 روی DER) — برای نام فایل و نمایش.</summary>
    private static string FingerprintOfSpki(string pemOrBase64)
    {
        var trimmed = pemOrBase64.Trim();
        byte[] der;
        if (trimmed.StartsWith("-----BEGIN", StringComparison.Ordinal))
        {
            var sb = new System.Text.StringBuilder();
            foreach (var line in trimmed.Split('\n'))
            {
                var l = line.Trim();
                if (l.Length == 0 || l.StartsWith("-----")) continue;
                sb.Append(l);
            }
            der = Convert.FromBase64String(sb.ToString());
        }
        else der = Convert.FromBase64String(new string(trimmed.Where(c => !char.IsWhiteSpace(c)).ToArray()));
        var hash = System.Security.Cryptography.SHA256.HashData(der);
        var hex = string.Concat(hash.Take(10).Select(b => b.ToString("X2")));
        return string.Join('-', new[] { hex[..8], hex.Substring(8, 8), hex[16..] });
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
        _lvHistory.Columns.Add("اثر انگشت سرور", 220);
        _lvHistory.Columns.Add("انقضا", 140);
        _lvHistory.Columns.Add("سقف پرسنل", 85);
        _lvHistory.Columns.Add("ماژول‌ها", 240);   // فاز ۷۲
        _lvHistory.Columns.Add("یادداشت", 240);
        _lvHistory.DoubleClick += (_, _) =>
        {
            if (_lvHistory.SelectedItems.Count == 0) return;
            if (_lvHistory.SelectedItems[0].Tag is LogEntry e && !string.IsNullOrEmpty(e.Content))
            {
                try { Clipboard.SetText(e.Content); }
                catch { }
                MessageBox.Show(this, "متن مجوز کپی شد — می‌توانید برای مشتری ارسال کنید", "کپی شد", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(_lvHistory, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    // ------------------------------------------------------------------
    // تب ۴ — راهنما (فاز ۷۲ب — بازطراحی خوانایی: تیترهای رنگی، فهرست‌های گلوله‌ای،
    // جمله‌های کوتاه و بدون اصطلاحات داخلی توسعه؛ متن یک‌جا به‌جای بلوک فشرده)
    // ------------------------------------------------------------------
    private TabPage BuildHelpTab()
    {
        var page = new TabPage("راهنما") { Padding = new Padding(10) };
        var rtb = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White,
            BorderStyle = BorderStyle.None, Font = Program.Vazir(11f), DetectUrls = false,
        };
        BuildHelpContent(rtb);
        rtb.SelectionStart = 0;
        page.Controls.Add(rtb);
        return page;
    }

    // ---------- رنگ‌ها و کمک‌کارهای نوشتن متن راهنما ----------
    private static readonly Color HelpHead = Color.FromArgb(15, 118, 110);   // سبزآبی برند
    private static readonly Color HelpBody = Color.FromArgb(45, 55, 65);     // خاکستری تیرهٔ خوانا
    private static readonly Color HelpMuted = Color.FromArgb(110, 120, 130); // توضیح کم‌رنگ

    /// <summary>یک تکه متن به انتهای جعبهٔ راهنما اضافه می‌کند و همان‌جا قالب‌بندی می‌شود.</summary>
    private static void AddHelpText(RichTextBox b, string text, Font? f = null, Color? c = null)
    {
        var start = b.TextLength;
        b.AppendText(text);
        b.Select(start, b.TextLength - start);
        if (f is not null) b.SelectionFont = f;
        if (c is not null) b.SelectionColor = c.Value;
        b.SelectionLength = 0;
    }

    /// <summary>تیتر بخش — با فاصلهٔ کافی از بخش قبلی، رنگ برند و درشت.</summary>
    private static void HelpTitle(RichTextBox b, string title, bool first = false)
    {
        if (!first) AddHelpText(b, "\n");
        AddHelpText(b, title, Program.Vazir(13.5f, FontStyle.Bold), HelpHead);
        AddHelpText(b, "\n");
    }

    /// <summary>بند ساده.</summary>
    private static void HelpP(RichTextBox b, string text)
    {
        AddHelpText(b, text, Program.Vazir(11f), HelpBody);
        AddHelpText(b, "\n");
    }

    /// <summary>گلوله — نشان سبزآبی + کلیدواژهٔ پررنگ + توضیح.</summary>
    private static void HelpBullet(RichTextBox b, string lead, string rest)
    {
        AddHelpText(b, "\u2022  ", Program.Vazir(11f, FontStyle.Bold), HelpHead);
        if (lead.Length > 0) AddHelpText(b, lead, Program.Vazir(11f, FontStyle.Bold), HelpBody);
        AddHelpText(b, rest, Program.Vazir(11f), HelpBody);
        AddHelpText(b, "\n");
    }

    /// <summary>گام شماره‌دار — عدد سبزآبی درشت + توضیح.</summary>
    private static void HelpStep(RichTextBox b, int n, string lead, string rest)
    {
        AddHelpText(b, Fingerprint.ToPersianDigits(n.ToString()) + ".  ", Program.Vazir(12f, FontStyle.Bold), HelpHead);
        if (lead.Length > 0) AddHelpText(b, lead, Program.Vazir(11f, FontStyle.Bold), HelpBody);
        AddHelpText(b, rest, Program.Vazir(11f), HelpBody);
        AddHelpText(b, "\n");
    }

    /// <summary>خط کد/انگلیسی — یکنواخت، چپ‌به‌راست ثابت با نویسهٔ LRM.</summary>
    private static void HelpCode(RichTextBox b, string code)
    {
        AddHelpText(b, "\u200E" + code + "\u200E\n", new Font("Consolas", 10.5f), Color.FromArgb(127, 63, 24));
    }

    private static void BuildHelpContent(RichTextBox b)
    {
        // ---------- سرصفحه ----------
        AddHelpText(b, "راهنمای صدور و مدیریت مجوز", Program.Vazir(16f, FontStyle.Bold), HelpHead);
        AddHelpText(b, "\n");
        AddHelpText(b, "سامانهٔ یکپارچه منابع انسانی — ساخت، بررسی و فعال‌سازی فایل مجوز (.dat)", Program.Vazir(10f), HelpMuted);
        AddHelpText(b, "\n");

        // ---------- خلاصهٔ سریع ----------
        HelpTitle(b, "۱)  خلاصهٔ سریع — کل ماجرا در سه گام", first: true);
        HelpStep(b, 1, "فایل درخواست مشتری: ", "مدیر سرور مشتری از صفحهٔ «مجوز نصب» فایل درخواست (.hrreq) را دانلود و برای شما می‌فرستد.");
        HelpStep(b, 2, "صدور در همین برنامه (ویزارد ۴ مرحله‌ای): ", "تب «صدور مجوز» ویزاردی است: ۱) کلید امضا ← ۲) سرور و مشتری (فایل درخواست) ← ۳) اعتبار و ماژول‌های خریداری‌شده ← ۴) صدور و تحویل. کافی است دکمهٔ «مرحلهٔ بعد» را دنبال کنید.");
        HelpStep(b, 3, "فعال‌سازی: ", "مدیر مشتری همان فایل را در صفحهٔ «مجوز نصب» سرور خود بارگذاری می‌کند — امضا تأیید و مجوز فوری فعال می‌شود.");

        // ---------- گام ۱ ----------
        HelpTitle(b, "۲)  گام اول — دریافت امضای سرور مشتری");
        HelpBullet(b, "صفحهٔ مجوز نصب: ", "مدیر مشتری با حساب «مدیر ارشد» وارد سامانهٔ نصب‌شده روی سرور می‌شود و به صفحهٔ «مجوز نصب» (مسیر /License) می‌رود.");
        HelpBullet(b, "دانلود فایل درخواست: ", "با دکمهٔ «دانلود فایل درخواست (.hrreq)» فایلی شامل امضای سخت‌افزاری همان سرور، نام شرکت و نسخهٔ برنامه ساخته می‌شود؛ این فایل را برای شما می‌فرستد.");
        HelpBullet(b, "تکرار نمی‌شود: ", "این امضا از مشخصات خود سرور (MachineGuid ویندوز یا machine-id لینوکس + نام ماشین) ساخته می‌شود و روی هر سرور یکتاست — مجوز هر سرور فقط برای همان سرور کار می‌کند.");

        // ---------- گام ۲ ----------
        HelpTitle(b, "۳)  گام دوم — صدور مجوز در همین برنامه (ویزارد ۴ مرحله‌ای)");
        HelpStep(b, 1, "کلید امضا: ", "اگر هنوز کلید ندارید، «🔑 ساخت جفت‌کلید جدید…» را بزنید (فقط بار اول). مسیر کلید یک‌بار برای همیشه به‌خاطر سپرده می‌شود؛ تا انتخاب نشود، «مرحلهٔ بعد» اجازهٔ عبور نمی‌دهد و دکمهٔ «انتخاب کلید خصوصی…» برجسته است. اگر کلید بارگذاری‌شده با کلید رسمی محصول یکی نباشد، هشدار نارنجی می‌بینید — یعنی مجوز صادره فقط روی سرورهایی معتبر است که کلید عمومی همان کلید در آن‌ها ثبت شده باشد.");
        HelpStep(b, 2, "سرور و مشتری: ", "دکمهٔ «بازکردن فایل درخواست مشتری (.hrreq)…» امضا و نام شرکت را خودکار پر می‌کند. اگر فایل در دسترس نیست، «چسباندن امضا» یا تایپ دستی هم پذیرفته است — فاصله‌ها و ارقام فارسی خودکار اصلاح می‌شوند.");
        HelpStep(b, 3, "اعتبار و ماژول‌ها: ", "مدت اعتبار از گزینه‌های آماده (دائمی، ۱ تا ۵ سال) یا «سفارشی…» با تاریخ شمسی؛ سقف پرسنل از گزینه‌های آماده یا سفارشی (صفر = بی‌نهایت)؛ و ماژول‌های خریداری‌شده — اگر مشتری همه را خریده «همهٔ ماژول‌ها» بماند، وگرنه «انتخاب محدود» را برگزینید و فقط همان‌ها را تیک بزنید (حداقل یک ماژول).");
        HelpStep(b, 4, "صدور و تحویل: ", "دکمهٔ «صدور مجوز» را بزنید، سپس «ذخیرهٔ فایل .dat…» یا «کپی متن مجوز» — فایل را برای مشتری بفرستید.");
        HelpBullet(b, "کنترل پیش از ارسال: ", "می‌توانید فایل ساخته‌شده را در تب «بررسی مجوز» باز کنید تا اعتبار امضا، تاریخ انقضا، ماژول‌های مجاز و «کدام کلید امضا را تأیید کرده» را ببینید.");
        HelpBullet(b, "تاریخچه: ", "هر صدور خودکار در تب «تاریخچهٔ صدور» ثبت می‌شود؛ دوبار کلیک روی هر سطر، متن همان مجوز را برای ارسال به مشتری کپی می‌کند.");

        // ---------- گام ۳ ----------
        HelpTitle(b, "۴)  گام سوم — فعال‌سازی روی سرور مشتری");
        HelpP(b, "مدیر مشتری در همان صفحهٔ «مجوز نصب»، فایل .dat را آپلود یا متن آن را می‌چسباند. سامانه امضا را با کلید عمومی جاسازی‌شده بررسی می‌کند و اگر امضای سرور با امضای درج‌شده در مجوز مطابق باشد، مجوز بلافاصله فعال می‌شود — بدون ری‌استارت.");

        // ---------- قفل ماژول‌ها ----------
        HelpTitle(b, "۵)  قفل ماژول‌ها — هر ماژول فقط با خرید");
        HelpP(b, "در حالت «انتخاب محدود»، فهرست ماژول‌های خریداری‌شده داخل فایل مجوز درج می‌شود و روی سرور مشتری این اتفاق می‌افتد:");
        HelpBullet(b, "", "هر ماژول خارج از فهرست — حتی اگر DLL آن روی سرور مشتری حاضر باشد — از منو حذف می‌شود و هیچ صفحه یا API از آن قابل استفاده نیست.");
        HelpBullet(b, "", "فعال‌سازی دستی همان ماژول از تنظیمات هم پذیرفته نمی‌شود (قفل با مجوز).");
        HelpBullet(b, "", "اعمال قفل بلافاصله و بدون ری‌استارت است؛ با ارتقای مجوز، ماژول‌های تازه همان لحظه برمی‌گردند.");
        HelpBullet(b, "کدهای ماژول‌ها: ", "org سازمان و پرسنل — attendance حضور و غیاب — access کنترل تردد — payroll حقوق و دستمزد — devices تجهیزات و گیت‌وی — gateway دروازهٔ دستگاه‌ها — parking پارکینگ — visitors بازدیدکنندگان.");

        // ---------- نکته‌های امنیتی ----------
        HelpTitle(b, "۶)  نکته‌های امنیتی");
        HelpBullet(b, "کلید خصوصی فقط نزد شماست: ", "فایل private.pem هرگز با مشتری یا داخل بستهٔ محصول منتشر نمی‌شود؛ بدون آن، هیچ‌کس نمی‌تواند مجوز جعلی بسازد (امضای RSA-2048 با SHA256 عملاً جعل‌ناپذیر است).");
        HelpBullet(b, "کپی بین سرورها بی‌فایده است: ", "فایل مجوز روی سرور دیگر کار نمی‌کند، چون امضای هر سرور یکتاست.");
        HelpBullet(b, "دستکاری بی‌فایده است: ", "تغییر هر جزء متن مجوز (سقف پرسنل، تاریخ، ماژول‌ها) امضا را باطل می‌کند و مجوز رد می‌شود.");
        HelpBullet(b, "چرخش کلید: ", "اگر لازم شد کلید عوض شود: کلید جدید بسازید، کلید عمومی جدید را در برنامهٔ وب جایگزین و منتشر کنید، سپس برای همهٔ مشتری‌ها مجوز جدید صادر کنید. دکمهٔ «کپی کلید عمومی برای برنامهٔ وب» همین کار را آسان می‌کند.");

        // ---------- حالت سخت‌گیرانه ----------
        HelpTitle(b, "۷)  بررسی مجوز با کدام کلید؟ (تب «بررسی مجوز»)");
        HelpP(b, "تب «بررسی مجوز» امضای هر فایل .dat را به ترتیب با همهٔ کلیدهای شناخته‌شده امتحان می‌کند:");
        HelpBullet(b, "", "کلید رسمی محصول (جاسازی‌شده در استودیو و برنامهٔ وب)؛");
        HelpBullet(b, "", "نیمهٔ عمومی کلید خصوصیِ بارگذاری‌شده در استودیو (private.pem فعلی)؛");
        HelpBullet(b, "", "public.pem کنار فایل اجرایی یا در پوشهٔ داده‌های برنامه (خروجی «ساخت جفت‌کلید جدید»).");
        HelpP(b, "سطر «تأیید امضا با» نشان می‌دهد کدام کلید امضا را تأیید کرده است. اگر با کلید محلی تأیید شد، این مجوز فقط روی سروری پذیرفته می‌شود که کلید عمومی همان کلید در آن ثبت باشد (ساخت جفت‌کلید در صفحهٔ «مجوز نصب» سرور، یا جاسازی کلید عمومی جدید در نسخهٔ جدید برنامهٔ وب). قبلاً مجوزهای امضاشده با کلید محلی به‌اشتباه «نا معتبر» گزارش می‌شدند — این اشکال رفع شده است.");

        HelpTitle(b, "۸)  حالت سخت‌گیرانه و انقضا (تنظیم روی سرور مشتری)");
        HelpP(b, "برای فعال‌شدن بررسی مجوز، در فایل appsettings.json سرور مشتری:");
        HelpCode(b, "\"License\": { \"Mode\": \"Strict\", \"TrialDays\": 30, \"ExpiredAction\": \"Warn\" }");
        HelpBullet(b, "هشدار (پیش‌فرض): ", "انقضای مجوز سامانه را از کار نمی‌اندازد؛ فقط پس از هر ورود صفحهٔ یادآوری نمایش داده می‌شود و بنر قرمز بالای صفحه‌ها می‌ماند تا مجوز تمدید شود.");
        HelpBullet(b, "توقف کامل: ", "با ExpiredAction=Block، پس از انقضا صفحات و API بسته می‌شوند و فقط صفحهٔ مجوز، ورود، و پل دریافت دادهٔ دستگاه‌ها/دوربین‌ها باز می‌مانند تا هیچ ترددی از دست نرود. این گزینه در سامانه از صفحهٔ «تنظیمات ← سیاست پس از انقضای مجوز» هم قابل انتخاب است.");
        HelpBullet(b, "", "دورهٔ آزمایشی ۳۰ روزه به خودِ سرور گره خورده است و دستکاری‌شدنش کشف می‌شود. حالت پیش‌فرض Off است.");

        // ---------- فایل‌ها ----------
        HelpTitle(b, "۹)  فایل‌ها و پوشه‌های این برنامه");
        HelpBullet(b, "پوشهٔ داده‌ها: ", "پوشهٔ کاربر ← HrLicenseStudio (گزارش صدور و پیکربندی) — دکمهٔ «بازکردن پوشهٔ گزارش‌ها» در تب تاریخچه، همین پوشه را باز می‌کند.");
        HelpBullet(b, "تاریخچهٔ صدور: ", "فایل issued-log.jsonl — دوبار کلیک روی هر سطر تاریخچه = کپی متن همان مجوز.");
        HelpBullet(b, "فونت برنامه: ", "وزیر (Vazirmatn) داخل خود برنامه جاسازی شده و نیازی به نصب فونت روی ویندوز نیست.");
    }
}

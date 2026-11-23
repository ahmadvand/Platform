using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LicenseStudio;

/// <summary>
/// پنجرهٔ اصلی استودیو مجوز نصب — فارسی/راست‌به‌چپ با فونت وزیر (فاز ۵۰).
/// تب‌ها: صدور مجوز | بررسی مجوز | تاریخچهٔ صدور | راهنما
/// فاز ۵۰: ورود آسان امضای سرور با «فایل درخواست» (.hrreq از صفحهٔ /License سرور مشتری)
/// و همهٔ فیلدها با گزینه‌های آمادهٔ قابل انتخاب (مدت اعتبار و سقف پرسنل).
/// نسخهٔ ۱.۶.۰ — بازگشت ویزارد صدور: تب صدور گام‌به‌گام شد (۱ کلید ← ۲ مشخصات مشتری
/// و اعتبار ← ۳ صدور و ذخیره) با نوار گام‌ها و دکمه‌های گام بعد/گام قبل.
/// </summary>
public partial class MainForm : Form
{
    // ---------- رنگ برند (فاز ۵۶) ----------
    private static readonly Color Brand = ColorTranslator.FromHtml("#0f766e");     // سبزآبی تیره
    private static readonly Color BrandSoft = ColorTranslator.FromHtml("#eef7f6"); // پس‌زمینهٔ ملایم

    /// <summary>فاز ۷۸ — همهٔ پیام‌های MessageBox راست‌چین و راست‌تراز نمایش داده می‌شوند.</summary>
    private const MessageBoxOptions RtlMsg = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

    // ---------- وضعیت کلید ----------
    private string? _privateKeyPem;
    private string? _privateKeyPath;
    private string? _loadedPublicSpki; // نیمهٔ عمومی کلید بارگذاری‌شده — برای نمایش

    // ---------- مسیرها ----------
    private static string AppDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LicenseStudio");
            Directory.CreateDirectory(dir);
            // مهاجرت یک‌باره از نام قدیمی (HrLicenseStudio) — صفر از دست رفتن تاریخچه/پیکربندی
            var old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HrLicenseStudio");
            if (Directory.Exists(old) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                try
                {
                    foreach (var f in Directory.GetFiles(old))
                        File.Move(f, Path.Combine(dir, Path.GetFileName(f)), overwrite: false);
                    Directory.Delete(old);
                }
                catch { /* مهاجرت اختیاری است — مسیر جدید همیشه کار می‌کند */ }
            }
            return dir;
        }
    }

    private static string LogPath => Path.Combine(AppDir, "issued-log.jsonl");
    private static string ConfigPath => Path.Combine(AppDir, "config.json");

    // ---------- کنترل‌های تب صدور ----------
    private TextBox _txtMachine = null!;
    private Label _lblFpState = null!;
    private Button _btnOpenReq = null!, _btnPasteFp = null!;
    private Label _lblReqInfo = null!;
    private TextBox _txtLicensee = null!;
    private ComboBox _cmbTerm = null!;                 // فاز ۵۰ — گزینه‌های آمادهٔ مدت اعتبار
    private ComboBox _cmbY = null!, _cmbM = null!, _cmbD = null!;
    private Label _lblGreg = null!;
    private ComboBox _cmbMaxPreset = null!;            // فاز ۵۰ — گزینه‌های آمادهٔ سقف پرسنل
    private NumericUpDown _numMax = null!;
    private ComboBox _cmbLockMode = null!;             // فاز ۷۸ — حالت اجرای قفل (انتخاب صریح صادرکننده)
    private CheckedListBox _clbModules = null!;        // فاز ۸۲ — انتخاب ماژول‌های مجاز (بازگردانی به درخواست کاربر)
    private TextBox _txtNotes = null!;
    private TextBox _txtOut = null!;
    private Button _btnIssue = null!, _btnSave = null!, _btnCopy = null!;
    private Label _lblIssueResult = null!;
    private Label _lblKeyState = null!;                        // فاز ۵۶ — وضعیت کلید داخل کادر کلید
    private Button _btnLoadKey = null!, _btnGenKey = null!;    // فاز ۵۶ — فیلد شدند (فوکوس/استایل پویا)

    // ---------- ویزارد صدور (نسخهٔ ۱.۶.۰ — بازگشت ویزارد) ----------
    private GroupBox _gbStep1 = null!, _gbStep2 = null!, _gbStep3 = null!;
    private Label _chip1 = null!, _chip2 = null!, _chip3 = null!;
    private Button _btnWizNext = null!, _btnWizBack = null!;
    private Label _lblWizPos = null!;
    private int _wizStep = 1;

    // ---------- گزینه‌های آماده (فاز ۵۰) ----------
    private static readonly string[] TermLabels =
        { "دائمی (بدون انقضا)", "۱ ماه", "۳ ماه", "۶ ماه", "۹ ماه", "۱ سال", "۲ سال", "۳ سال", "۵ سال", "سفارشی…" };
    private static readonly int[] TermMonths = { 0, 1, 3, 6, 9, 12, 24, 36, 60, -1 };
    private const int TermPermanent = 0, TermCustom = 9;

    private static readonly string[] MaxLabels =
        { "بدون محدودیت", "۱۰ نفر", "۲۵ نفر", "۵۰ نفر", "۱۰۰ نفر", "۲۵۰ نفر", "۵۰۰ نفر", "۱۰۰۰ نفر", "۵۰۰۰ نفر", "سفارشی…" };
    private static readonly int[] MaxValues = { 0, 10, 25, 50, 100, 250, 500, 1000, 5000, -1 };
    private const int MaxUnlimited = 0, MaxCustom = 9;

    // ---------- کنترل‌های تب بررسی ----------
    private TextBox _txtContent = null!;
    private TextBox _txtRefFp = null!;
    private ListView _lvInspect = null!;
    private Label _lblInspectVerdict = null!;
    private Label _lblPubState = null!;      // فاز ۷۸ — وضعیت کلید عمومی فعال برای بررسی
    private Button _btnResetPub = null!;
    private string? _customPubKey;           // فاز ۷۸ — کلید عمومی دلخواه (PEM یا SPKI)؛ null = کلید سازنده

    // ---------- کنترل‌های تب تاریخچه ----------
    private ListView _lvHistory = null!;

    // ---------- نوار وضعیت ----------
    private ToolStripStatusLabel _stKey = null!;

    public MainForm()
    {
        Text = "استودیو مجوز نصب — سامانه یکپارچه منابع انسانی";
        Font = Program.Vazir(10f); // فاز ۵۰ — فونت وزیر جاسازی‌شده
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1060, 760);
        MinimumSize = new Size(940, 660);

        BuildUi();
        TryLoadKey(auto: true);
        RefreshHistory();
    }

    // ==================================================================
    // ساخت رابط کاربری
    // ==================================================================
    private void BuildUi()
    {
        // فاز ۵۶ — بنر عنوان با رنگ برند (شناسهٔ برنامه + نسخه از اسمبلی)
        var banner = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Brand };
        var lblTitle = new Label
        {
            Text = "🔑 استودیو مجوز نصب", ForeColor = Color.White,
            Font = Program.Vazir(14f, FontStyle.Bold), AutoSize = true, Location = new Point(18, 6),
        };
        var lblSub = new Label
        {
            Text = "ساخت، بررسی و تاریخچهٔ فایل مجوز (.dat) — نسخهٔ " + Fingerprint.ToPersianDigits(Program.VersionText),
            ForeColor = Color.FromArgb(213, 237, 235), Font = Program.Vazir(9f), AutoSize = true, Location = new Point(20, 33),
        };
        banner.Controls.AddRange(new Control[] { lblTitle, lblSub });

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Program.Vazir(10.5f) };
        tabs.TabPages.Add(BuildIssueTab());
        tabs.TabPages.Add(BuildInspectTab());
        tabs.TabPages.Add(BuildHistoryTab());
        tabs.TabPages.Add(BuildHelpTab());

        var st = new StatusStrip { Font = Program.Vazir(9f) };
        _stKey = new ToolStripStatusLabel("کلید: در حال بررسی…") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
        var stVer = new ToolStripStatusLabel("نسخهٔ " + Fingerprint.ToPersianDigits(Program.VersionText));
        st.Items.Add(_stKey);
        st.Items.Add(new ToolStripStatusLabel("  |  "));
        st.Items.Add(stVer);

        Controls.Add(tabs);
        Controls.Add(st);
        Controls.Add(banner); // ترتیب Dock: بنر بالا، نوار وضعیت پایین، تب‌ها میان آن دو
    }

    // ------------------------------------------------------------------
    // تب ۱ — صدور مجوز
    // ------------------------------------------------------------------
    private TabPage BuildIssueTab()
    {
        var page = new TabPage("صدور مجوز") { Padding = new Padding(12), AutoScroll = true };

        // فاز ۷۴ — ریشهٔ «ناپدید شدن دکمه‌ها در تمام‌صفحه»: ریشهٔ Dock.Fill با ردیف
        // Percent-100 همیشه «جا می‌شود» (AutoScroll هرگز فعال نمی‌شود) و ردیف انعطاف‌پذیر
        // محتوای میانی را می‌بلعد. اصلاح: ریشهٔ Dock.Top + AutoSize (ارتفاع = جمع واقعی
        // محتوا) + همهٔ ردیف‌ها AutoSize + MinimumSize فقط محدودِ عرض (۹۲۰) — پنجرهٔ
        // کوچک/بزرگ و هر DPI: همهٔ بخش‌ها و دکمه‌ها همیشه دیده می‌شوند.
        var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 5 };
        root.MinimumSize = new Size(920, 0);
        for (var i = 0; i < 5; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // ---------- نوار گام‌های ویزارد (نسخهٔ ۱.۶.۰ — بازگشت ویزارد) ----------
        // سه گام قابل‌کلیک؛ گام جاری برجستهٔ برند است. کلیک روی هر گام = پرش (پرش رو به
        // جلو با همان اعتبارسنجیِ «گام بعد»، پرش رو به عقب آزاد).
        var chips = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Padding = new Padding(0, 0, 0, 6) };
        _chip1 = MakeChip("۱ · کلید امضا", 1);
        _chip2 = MakeChip("۲ · مشخصات مشتری و اعتبار", 2);
        _chip3 = MakeChip("۳ · صدور و ذخیره", 3);
        chips.Controls.AddRange(new Control[] { _chip1, _chip2, _chip3 });

        // ---------- گام ۱: کلید (فاز ۵۶ — بازطراحی: وضعیت داخل کادر + دکمهٔ همه‌جادار) ----------
        // قبلاً: برچسب بلند + ۳ دکمه در یک ردیف بدون شکستن (WrapContents=false) → در DPI/عرض
        // کمتر، دکمه‌ها از کادر بیرون زده و دیده نمی‌شدند؛ کاربر پیام قرمز «کلید خصوصی لازم
        // است» را پایین فرم می‌دید ولی جای انتخاب کلید را پیدا نمی‌کرد.
        var gbKey = new GroupBox { Text = "گام ۱ — کلید امضای مجوز (خصوصی)", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10) };
        _gbStep1 = gbKey;
        var keyTbl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3 };
        keyTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        keyTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        keyTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // ردیف وضعیت — پیام «کلید لازم است» حالا همین‌جاست، دقیقاً کنار راه‌حلش
        _lblKeyState = new Label
        {
            Text = "", AutoSize = true, Padding = new Padding(2, 2, 2, 4),
            Font = Program.Vazir(10f, FontStyle.Bold),
        };
        keyTbl.Controls.Add(_lblKeyState, 0, 0);

        // ردیف دکمه‌ها — WrapContents=true تا در هیچ عرض/DPI از کادر بیرون نزنند
        _btnLoadKey = new Button { Text = "انتخاب کلید خصوصی (private.pem)…", AutoSize = true, MinimumSize = new Size(0, 34) };
        _btnGenKey = new Button { Text = "🔑 ساخت جفت‌کلید جدید…", AutoSize = true, MinimumSize = new Size(0, 34), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        var btnShowPub = new Button { Text = "کپی کلید عمومی برای برنامهٔ وب", AutoSize = true, MinimumSize = new Size(0, 34), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        _btnLoadKey.Click += (_, _) => PickPrivateKey();
        _btnGenKey.Click += (_, _) => GenerateKeyPair();
        btnShowPub.Click += (_, _) =>
        {
            var pub = _loadedPublicSpki ?? EmbeddedKeys.PublicKeySpki;
            try { Clipboard.SetText(pub); MessageBox.Show(this, "کلید عمومی (SPKI/Base64) کپی شد — برای جاسازی در LicenseService.cs استفاده کنید", "کپی شد", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, RtlMsg); }
            catch { }
        };
        var keyBtns = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };
        keyBtns.Controls.AddRange(new Control[] { _btnLoadKey, _btnGenKey, btnShowPub });
        keyTbl.Controls.Add(keyBtns, 0, 1);

        var keyHint = new Label
        {
            Text = "مجوز با کلید خصوصی امضا می‌شود؛ کلید عمومیِ همان کلید داخل برنامهٔ وب جاسازی شده است. مسیر کلید یک‌بار برای همیشه به‌خاطر سپرده می‌شود.",
            AutoSize = true, Padding = new Padding(0, 4, 0, 0), ForeColor = Color.DimGray,
        };
        keyTbl.Controls.Add(keyHint, 0, 2);
        gbKey.Controls.Add(keyTbl);

        // ---------- گام ۲: مشخصات مشتری و اعتبار — فاز ۷۴: بخش خودتراست (Top+AutoSize) ----------
        var gbForm = new GroupBox { Text = "گام ۲ — مشخصات مشتری و اعتبار", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        _gbStep2 = gbForm;
        var tbl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 9 };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 9; i++) tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // امضای سرور (اثر انگشت)
        _txtMachine = new TextBox { Font = new Font("Consolas", 11f), Width = 300, RightToLeft = RightToLeft.No };
        _lblFpState = new Label { Text = "", AutoSize = true, Padding = new Padding(8, 8, 0, 0), Font = Program.Vazir(10f, FontStyle.Bold) };
        _btnPasteFp = new Button { Text = "چسباندن امضا", AutoSize = true };
        _txtMachine.TextChanged += (_, _) =>
        {
            var flat = Fingerprint.Normalize(_txtMachine.Text);
            if (flat.Length > 29) flat = flat[..29];
            if (!string.Equals(_txtMachine.Text, flat, StringComparison.Ordinal) && flat.Length is 0 or 29)
            {
                _txtMachine.Text = flat; _txtMachine.SelectionStart = flat.Length;
            }
            _lblFpState.Text = Fingerprint.IsValid(_txtMachine.Text) ? "✓" : "";
            _lblFpState.ForeColor = Color.SeaGreen;
        };
        _btnPasteFp.Click += (_, _) => ApplyRequest(TryGetClipboardText());
        var fpPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        fpPanel.Controls.Add(_txtMachine);
        fpPanel.Controls.Add(_lblFpState);
        fpPanel.Controls.Add(_btnPasteFp);
        tbl.Controls.Add(MakeLabel("امضای سرور *"), 0, 0);
        tbl.Controls.Add(fpPanel, 1, 0);

        // فایل درخواست مشتری (فاز ۵۰) — بدون تایپ دستی
        _btnOpenReq = new Button
        {
            Text = "بازکردن فایل درخواست مشتری (.hrreq)…", AutoSize = true,
            BackColor = ColorTranslator.FromHtml("#eef7f6"), FlatStyle = FlatStyle.Flat,
        };
        _lblReqInfo = new Label
        {
            Text = "فایل .hrreq را از صفحهٔ «مجوز نصب» سرور مشتری (دکمهٔ دانلود فایل درخواست) بگیرید — امضا و نام شرکت خودکار پر می‌شود",
            AutoSize = true, Padding = new Padding(10, 8, 0, 0), ForeColor = Color.DimGray,
        };
        _btnOpenReq.Click += (_, _) => OpenRequestFile();
        var reqPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        reqPanel.Controls.Add(_btnOpenReq);
        reqPanel.Controls.Add(_lblReqInfo);
        tbl.Controls.Add(MakeLabel("فایل درخواست"), 0, 1);
        tbl.Controls.Add(reqPanel, 1, 1);

        // دارنده
        _txtLicensee = new TextBox { Dock = DockStyle.Top };
        tbl.Controls.Add(MakeLabel("دارندهٔ مجوز (شرکت/مشتری) *"), 0, 2);
        tbl.Controls.Add(_txtLicensee, 1, 2);

        // مدت اعتبار — گزینه‌های آماده (فاز ۵۰)
        _cmbTerm = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        foreach (var t in TermLabels) _cmbTerm.Items.Add(t);
        _lblGreg = new Label { Text = "", AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(10, 8, 0, 0) };
        var termPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        termPanel.Controls.Add(_cmbTerm);
        termPanel.Controls.Add(_lblGreg);
        tbl.Controls.Add(MakeLabel("مدت اعتبار"), 0, 3);
        tbl.Controls.Add(termPanel, 1, 3);

        // تاریخ شمسی — فقط برای «سفارشی» فعال می‌شود (فاز ۵۰)
        _cmbY = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 86, Enabled = false };
        _cmbM = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Enabled = false };
        _cmbD = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 66, Enabled = false };
        var datePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        datePanel.Controls.AddRange(new Control[] { _cmbY, _cmbM, _cmbD });
        _cmbY.SelectedIndexChanged += (_, _) => { FillDays(); UpdateGregLabel(); };
        _cmbM.SelectedIndexChanged += (_, _) => { FillDays(); UpdateGregLabel(); };
        _cmbD.SelectedIndexChanged += (_, _) => UpdateGregLabel();
        tbl.Controls.Add(MakeLabel("تاریخ انقضا (شمسی)"), 0, 4);
        tbl.Controls.Add(datePanel, 1, 4);

        // سقف پرسنل — گزینه‌های آماده (فاز ۵۰)
        _cmbMaxPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        foreach (var m in MaxLabels) _cmbMaxPreset.Items.Add(m);
        _numMax = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = 0, Width = 120, TextAlign = HorizontalAlignment.Center, Enabled = false };
        var maxHint = new Label { Text = "۰ = بدون محدودیت پرسنل", AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(10, 8, 0, 0) };
        var maxPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        maxPanel.Controls.Add(_cmbMaxPreset);
        maxPanel.Controls.Add(_numMax);
        maxPanel.Controls.Add(maxHint);
        tbl.Controls.Add(MakeLabel("سقف پرسنل"), 0, 5);
        tbl.Controls.Add(maxPanel, 1, 5);

        // حالت اجرای قفل (فاز ۷۸) — انتخاب صریح صادرکنندهٔ مجوز؛ پیش‌فرض = طبق تنظیمات سرور مشتری
        _cmbLockMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
        foreach (var l in LicenseLockMode.Labels) _cmbLockMode.Items.Add(l);
        var lockHint = new Label
        {
            Text = "درج‌شده در خود فایل مجوز است و روی سرور مشتری بر تنظیمات appsettings آن اولویت دارد",
            AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(10, 8, 0, 0),
        };
        var lockPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        lockPanel.Controls.Add(_cmbLockMode);
        lockPanel.Controls.Add(lockHint);
        tbl.Controls.Add(MakeLabel("حالت اجرای قفل"), 0, 6);
        tbl.Controls.Add(lockPanel, 1, 6);

        // انتخاب ماژول‌های مجاز (فاز ۸۲ — بازگردانی به درخواست کاربر؛ سرور ماژول بیرون از فهرست را قفل می‌کند)
        _clbModules = new CheckedListBox
        {
            CheckOnClick = true, Dock = DockStyle.Top, MinimumSize = new Size(0, 112),
            ThreeDCheckBoxes = true,
};
        foreach (var m in LicenseModules.All) _clbModules.Items.Add(m.Code + " — " + m.Title, false);
        // فاز ۸۳ — انتخاب سریع «همه / هیچ» برای ماژول‌ها (درخواست کاربر)
        var btnAllMods = new Button { Text = "انتخاب همه", AutoSize = true, MinimumSize = new Size(0, 30), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        var btnNoneMods = new Button { Text = "انتخاب هیچ", AutoSize = true, MinimumSize = new Size(0, 30), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        btnAllMods.Click += (_, _) => { for (var i = 0; i < _clbModules.Items.Count; i++) _clbModules.SetItemChecked(i, true); };
        btnNoneMods.Click += (_, _) => { for (var i = 0; i < _clbModules.Items.Count; i++) _clbModules.SetItemChecked(i, false); };
        var modulesBtnRow = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true };
        modulesBtnRow.Controls.Add(btnAllMods);
        modulesBtnRow.Controls.Add(btnNoneMods);
        var modulesHint = new Label
        {
            Text = "هیچ ماژولی تیک نخورد = همهٔ ماژول‌ها — ماژول‌های تیک‌خورده داخل خود فایل مجوز درج می‌شوند و روی سرور مشتری فقط همان‌ها فعال می‌مانند",
            AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(10, 6, 0, 0),
        };
        var modulesPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
        modulesPanel.Controls.Add(_clbModules);
        modulesPanel.Controls.Add(modulesBtnRow);
        modulesPanel.Controls.Add(modulesHint);
        tbl.Controls.Add(MakeLabel("ماژول‌های مجاز"), 0, 7);
        tbl.Controls.Add(modulesPanel, 1, 7);

        // یادداشت
        _txtNotes = new TextBox { Dock = DockStyle.Top, PlaceholderText = "مثلاً: مجوز سالانهٔ ۱۴۰۶ — سفارش ۱۲۳۴" };
        tbl.Controls.Add(MakeLabel("یادداشت"), 0, 8);
        tbl.Controls.Add(_txtNotes, 1, 8);
        gbForm.Controls.Add(tbl);

        // رویداد گزینه‌های آماده — پس از ساخته‌شدن همهٔ کنترل‌ها
        _cmbTerm.SelectedIndexChanged += (_, _) => OnTermChanged();
        _cmbMaxPreset.SelectedIndexChanged += (_, _) => OnMaxPresetChanged();

        // ---------- گام ۳: صدور و ذخیره — فاز ۷۴: ارتفاع طبیعی قفل‌شده + دکمه‌های شکستنی ----------
        var gbOut = new GroupBox { Text = "گام ۳ — صدور و ذخیرهٔ فایل مجوز (.dat)", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        _gbStep3 = gbOut;
        var outTbl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        outTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _txtOut = new TextBox
        {
            Dock = DockStyle.Top, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9.5f), RightToLeft = RightToLeft.No, BackColor = Color.White,
            MinimumSize = new Size(0, 150),   // ارتفاع طبیعی — نه صفر، نه بلعیده‌شده
        };
        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 8, 0, 0) };
        _btnIssue = new Button { Text = "صدور مجوز", BackColor = Brand, ForeColor = Color.White, Font = Program.Vazir(10f, FontStyle.Bold), Size = new Size(140, 34), FlatStyle = FlatStyle.Flat };
        _btnSave = new Button { Text = "ذخیرهٔ فایل .dat…", Size = new Size(150, 34), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat, Enabled = false };
        _btnCopy = new Button { Text = "کپی متن مجوز", Size = new Size(130, 34), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat, Enabled = false };
        _lblIssueResult = new Label { Text = "", AutoSize = true, Padding = new Padding(12, 10, 0, 0), Font = Program.Vazir(10f, FontStyle.Bold) };
        _btnIssue.Click += (_, _) => DoIssue();
        _btnSave.Click += (_, _) => DoSave();
        _btnCopy.Click += (_, _) =>
        {
            if (_txtOut.Text.Length == 0) return;
            try { Clipboard.SetText(_txtOut.Text); _lblIssueResult.Text = "کپی شد ✓"; _lblIssueResult.ForeColor = Color.SeaGreen; }
            catch { }
        };
        btnRow.Controls.AddRange(new Control[] { _btnIssue, _btnSave, _btnCopy, _lblIssueResult });
        outTbl.Controls.Add(_txtOut, 0, 0);
        outTbl.Controls.Add(btnRow, 0, 1);
        gbOut.Controls.Add(outTbl);

        // ---------- نوار پیمایش ویزارد (گام بعد / گام قبل) ----------
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
        _btnWizBack = new Button { Text = "گام قبل", Size = new Size(120, 34), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f), Enabled = false };
        _btnWizNext = new Button { Text = "گام بعد", Size = new Size(120, 34), BackColor = Brand, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f, FontStyle.Bold) };
        _lblWizPos = new Label { Text = "", AutoSize = true, Padding = new Padding(12, 9, 12, 0), ForeColor = Color.DimGray, Font = Program.Vazir(10f) };
        _btnWizNext.Click += (_, _) => TryGoToStep(_wizStep + 1);
        _btnWizBack.Click += (_, _) => ShowStep(_wizStep - 1);
        nav.Controls.AddRange(new Control[] { _btnWizBack, _btnWizNext, _lblWizPos });

        root.Controls.Add(chips, 0, 0);
        root.Controls.Add(gbKey, 0, 1);
        root.Controls.Add(gbForm, 0, 2);
        root.Controls.Add(gbOut, 0, 3);
        root.Controls.Add(nav, 0, 4);
        page.Controls.Add(root);

        InitDateDefaults();
        _cmbTerm.SelectedIndex = TermPermanent;   // پیش‌فرض مثل قبل: دائمی
        _cmbMaxPreset.SelectedIndex = MaxUnlimited;
        _cmbLockMode.SelectedIndex = 0;           // فاز ۷۸ — پیش‌فرض: طبق تنظیمات سرور مشتری
        ShowStep(1);                              // نسخهٔ ۱.۶.۰ — ویزارد همیشه از گام ۱ شروع می‌شود
        return page;
    }

    // ------------------------------------------------------------------
    // ویزارد صدور (نسخهٔ ۱.۶.۰) — نوار گام‌ها، پیمایش و اعتبارسنجی هر گام
    // ------------------------------------------------------------------
    /// <summary>ساخت چیپ یک گام — قابل کلیک، استایل گام جاری در ShowStep اعمال می‌شود.</summary>
    private Label MakeChip(string text, int n)
    {
        var c = new Label
        {
            Text = text, AutoSize = true, Padding = new Padding(12, 8, 12, 8), Margin = new Padding(4, 2, 4, 2),
            Cursor = Cursors.Hand, BackColor = BrandSoft, ForeColor = Color.FromArgb(31, 41, 55),
            Font = Program.Vazir(10f),
        };
        c.Click += (_, _) => TryGoToStep(n);
        return c;
    }

    /// <summary>نمایش گام n — پنهان‌سازی بقیهٔ گام‌ها (ردیف AutoSize جمع می‌شود)، استایل چیپ‌ها و وضعیت دکمه‌ها.</summary>
    private void ShowStep(int n)
    {
        _wizStep = Math.Clamp(n, 1, 3);
        _gbStep1.Visible = _wizStep == 1;
        _gbStep2.Visible = _wizStep == 2;
        _gbStep3.Visible = _wizStep == 3;
        StyleChip(_chip1, 1); StyleChip(_chip2, 2); StyleChip(_chip3, 3);
        _lblWizPos.Text = "گام " + Fingerprint.ToPersianDigits(_wizStep.ToString()) + " از ۳";
        _btnWizBack.Enabled = _wizStep > 1;
        _btnWizNext.Visible = _btnWizNext.Enabled = _wizStep < 3;
    }

    private void StyleChip(Label chip, int n)
    {
        var current = n == _wizStep;
        chip.BackColor = current ? Brand : BrandSoft;
        chip.ForeColor = current ? Color.White : Color.FromArgb(31, 41, 55);
        chip.Font = Program.Vazir(10f, current ? FontStyle.Bold : FontStyle.Regular);
    }

    /// <summary>پرش به گام هدف — هر گام میانی رو به جلو با اعتبارسنجی همان گام؛ رو به عقب آزاد.</summary>
    private void TryGoToStep(int target)
    {
        target = Math.Clamp(target, 1, 3);
        if (target == _wizStep) { ShowStep(target); return; }
        if (target > _wizStep)
            for (var s = _wizStep; s < target; s++)
                if (!CanLeaveStep(s)) return;
        ShowStep(target);
    }

    /// <summary>شرط ترک گام s به سمت جلو — پیام + فوکوس روی فیلد مشکل‌دار (الگوی فاز ۵۶).</summary>
    private bool CanLeaveStep(int s)
    {
        if (s == 1 && _privateKeyPem is null)
        {
            MessageBox.Show(this,
                "برای رفتن به گام بعد، ابتدا کلید خصوصی (private.pem) را انتخاب کنید یا جفت‌کلید جدید بسازید — دکمهٔ برجستهٔ «انتخاب کلید خصوصی…» همین‌جاست.",
                "کلید لازم است", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, RtlMsg);
            _btnLoadKey.Focus();
            return false;
        }
        if (s != 2) return true;

        var fp = Fingerprint.Normalize(_txtMachine.Text);
        var fpErr = Fingerprint.ValidationError(fp);
        if (fpErr is not null)
        {
            MessageBox.Show(this, fpErr + "\nقالب درست: " + Fingerprint.Pattern + "\nسریع‌ترین راه: «بازکردن فایل درخواست مشتری» یا «چسباندن امضا»",
                "امضای سرور نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, RtlMsg);
            _txtMachine.Focus();
            return false;
        }
        if (string.IsNullOrWhiteSpace(_txtLicensee.Text))
        {
            MessageBox.Show(this, "نام دارندهٔ مجوز (شرکت/مشتری) را بنویسید — از فایل .hrreq هم خودکار پر می‌شود.",
                "دارندهٔ مجوز لازم است", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, RtlMsg);
            _txtLicensee.Focus();
            return false;
        }
        return true;
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text, AutoSize = true, Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 10, 8, 4),
    };

    // ==================================================================
    // منطق گزینه‌های آماده (فاز ۵۰)
    // ==================================================================
    private void OnTermChanged()
    {
        var t = _cmbTerm.SelectedIndex;
        if (t < 0) return;
        if (t == TermPermanent)
        {
            _cmbY.Enabled = _cmbM.Enabled = _cmbD.Enabled = false;
            _lblGreg.Text = "— مجوز بدون تاریخ انقضا صادر می‌شود";
            return;
        }
        _cmbY.Enabled = _cmbM.Enabled = _cmbD.Enabled = t == TermCustom;
        if (t == TermCustom)
        {
            // فاز ۵۱ — رفع «نمیشه زمان رو تعیین کرد»: با انتخاب سفارشی، تاریخ همیشه
            // انتخاب معتبر دارد (از امروز) و هر سه کمبوباکس فعال و قابل تعیین‌اند
            EnsureDateSelection();
        }
        else
        {
            // پرکردن خودکار تاریخ شمسی از امروز + مدت انتخابی
            var (ty, tm, td) = JalaliDates.ToJalali(DateTime.UtcNow);
            var (ny, nm, nd) = AddMonthsJalali(ty, tm, td, TermMonths[t]);
            SetDate(ny, nm, nd);
        }
        UpdateGregLabel();
    }

    /// <summary>تضمین انتخاب معتبر روی هر سه کمبوباکس تاریخ (فاز ۵۱) — پیش‌فرض: یک سال بعد.</summary>
    private void EnsureDateSelection()
    {
        var (ty, tm, td) = JalaliDates.ToJalali(DateTime.UtcNow);
        if (_cmbY.SelectedIndex < 0 && _cmbY.Items.Count > 0)
            _cmbY.SelectedIndex = ClampIndex(_cmbY, ty + 1);
        if (_cmbM.SelectedIndex < 0 && _cmbM.Items.Count > 0)
            _cmbM.SelectedIndex = Math.Clamp(tm - 1, 0, _cmbM.Items.Count - 1);
        FillDays();
        if (_cmbD.SelectedIndex < 0 && _cmbD.Items.Count > 0)
            _cmbD.SelectedIndex = Math.Clamp(td - 1, 0, _cmbD.Items.Count - 1);
    }

    /// <summary>ست هم‌زمان سال/ماه/روز با همگام‌سازی روزها — بدون خطای ایندکس.</summary>
    private void SetDate(int y, int m, int d)
    {
        if (_cmbY.Items.Count > 0) _cmbY.SelectedIndex = ClampIndex(_cmbY, y);
        if (_cmbM.Items.Count > 0) _cmbM.SelectedIndex = Math.Clamp(m - 1, 0, _cmbM.Items.Count - 1);
        FillDays();
        if (_cmbD.Items.Count > 0) _cmbD.SelectedIndex = Math.Clamp(d - 1, 0, _cmbD.Items.Count - 1);
    }

    private void OnMaxPresetChanged()
    {
        var t = _cmbMaxPreset.SelectedIndex;
        if (t < 0) return;
        if (t == MaxCustom)
        {
            _numMax.Enabled = true;
            return;
        }
        _numMax.Enabled = false;
        _numMax.Value = Math.Clamp(MaxValues[t], 0, _numMax.Maximum);
    }

    /// <summary>جمع ماه شمسی با سقف روز ماه مقصد (مثل ۳۱ → ۳۰ اسفند).</summary>
    private static (int Y, int M, int D) AddMonthsJalali(int y, int m, int d, int months)
    {
        var total = y * 12 + (m - 1) + months;
        var ny = total / 12;
        var nm = total % 12 + 1;
        var nd = Math.Min(d, JalaliDates.DaysInMonth(ny, nm));
        return (ny, nm, nd);
    }

    // ==================================================================
    // فایل درخواست مشتری (فاز ۵۰) — .hrreq از صفحهٔ /License سرور مشتری
    // ==================================================================
    private void OpenRequestFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "بازکردن فایل درخواست مجوز مشتری",
            Filter = "فایل درخواست مجوز (*.hrreq;*.json;*.txt)|*.hrreq;*.json;*.txt|همهٔ فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        ApplyRequest(File.ReadAllText(dlg.FileName));
    }

    /// <summary>امضا را از JSON فایل درخواست (یا متن خام اثر انگشت) می‌خواند و فرم را پر می‌کند.</summary>
    private void ApplyRequest(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        string? machine = null, company = null, version = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            var r = doc.RootElement;
            foreach (var k in new[] { "machine", "Machine", "signature", "fingerprint" })
                if (r.TryGetProperty(k, out var m) && m.ValueKind == JsonValueKind.String) { machine = m.GetString(); break; }
            foreach (var k in new[] { "company", "Company", "licensee" })
                if (r.TryGetProperty(k, out var c) && c.ValueKind == JsonValueKind.String) { company = c.GetString(); break; }
            foreach (var k in new[] { "version", "Version" })
                if (r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String) { version = v.GetString(); break; }
        }
        catch
        {
            machine = text; // JSON نبود — متن ساده = خود اثر انگشت
        }

        var fp = Fingerprint.Normalize(machine);
        var err = Fingerprint.ValidationError(fp);
        if (err is not null)
        {
            _lblReqInfo.Text = "✗ امضای سرور در فایل/متن معتبر نیست — " + err;
            _lblReqInfo.ForeColor = Color.Firebrick;
            return;
        }

        _txtMachine.Text = fp;
        if (!string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(_txtLicensee.Text))
            _txtLicensee.Text = company.Trim();
        _lblReqInfo.Text = "✓ امضا دریافت شد" +
            (string.IsNullOrWhiteSpace(company) ? "" : " — " + company.Trim()) +
            (string.IsNullOrWhiteSpace(version) ? "" : " (نسخهٔ سرور: " + version.Trim() + ")");
        _lblReqInfo.ForeColor = Color.SeaGreen;
    }

    private static string? TryGetClipboardText()
    {
        try { return Clipboard.GetText(); }
        catch { return null; }
    }

    // ==================================================================
    // تاریخ شمسی
    // ==================================================================
    private void InitDateDefaults()
    {
        _cmbY.Items.Clear();
        var (ty, _, _) = JalaliDates.ToJalali(DateTime.UtcNow);
        for (var y = ty - 1; y <= ty + 12; y++) _cmbY.Items.Add(Fingerprint.ToPersianDigits(y.ToString()));
        foreach (var m in JalaliDates.MonthNames) _cmbM.Items.Add(m);

        // پیش‌فرض: یک سال بعد از امروز (با سقف روز) — فاز ۵۱: از SetDate مشترک
        var t = DateTime.UtcNow;
        var (y2, m2, d2) = JalaliDates.ToJalali(t);
        SetDate(y2 + 1, m2, d2);
        UpdateGregLabel();
    }

    private static int ClampIndex(ComboBox cmb, int value)
    {
        for (var i = 0; i < cmb.Items.Count; i++)
            if (Fingerprint.ToPersianDigits(value.ToString()).Equals(cmb.Items[i]))
                return i;
        return cmb.Items.Count - 1;
    }

    private (int Y, int M, int D) SelectedJalali() => (
        ParsePersian(_cmbY.SelectedItem?.ToString()),
        _cmbM.SelectedIndex + 1,
        _cmbD.SelectedIndex + 1);

    private static int ParsePersian(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var sb = new StringBuilder();
        foreach (var ch in s) sb.Append(ch is >= '\u06F0' and <= '\u06F9' ? (char)('0' + (ch - '\u06F0')) : ch);
        return int.TryParse(sb.ToString(), out var n) ? n : 0;
    }

    private void FillDays()
    {
        var (y, m, _) = SelectedJalali();
        if (y <= 0 || m <= 0) return;
        var days = JalaliDates.DaysInMonth(y, m);
        var old = _cmbD.SelectedIndex;
        _cmbD.Items.Clear();
        for (var d = 1; d <= days; d++) _cmbD.Items.Add(Fingerprint.ToPersianDigits(d.ToString()));
        if (old >= 0) _cmbD.SelectedIndex = Math.Min(old, days - 1);
    }

    private void UpdateGregLabel()
    {
        if (_cmbTerm.SelectedIndex == TermPermanent)
        {
            _lblGreg.Text = "— مجوز بدون تاریخ انقضا صادر می‌شود";
            return;
        }
        var (y, m, d) = SelectedJalali();
        var g = JalaliDates.FromJalali(y, m, d);
        _lblGreg.Text = g is null ? "تاریخ نامعتبر" : $"میلادی: {g.Value.ToLocalTime().yyyy_MM_dd()} — اعتبار تا پایان همین روز";
    }

    // ==================================================================
    // کلید خصوصی
    // ==================================================================
    private void TryLoadKey(bool auto)
    {
        // ۱) مسیر ذخیره‌شدهٔ قبلی  ۲) کنار فایل اجرایی  ۳) پوشهٔ کاربر
        string? path = null;
        try
        {
            if (File.Exists(ConfigPath))
            {
                var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ConfigPath));
                if (cfg is { } && cfg.TryGetValue("KeyPath", out var p) && File.Exists(p)) path = p;
            }
            if (path is null)
            {
                var beside = Path.Combine(AppContext.BaseDirectory, "private.pem");
                if (File.Exists(beside)) path = beside;
                else
                {
                    var inApp = Path.Combine(AppDir, "private.pem");
                    if (File.Exists(inApp)) path = inApp;
                }
            }
        }
        catch { }

        if (path is null)
        {
            SetKeyState(null, auto
                ? "کلید خصوصی بارگذاری نشده — برای صدور مجوز، دکمهٔ «انتخاب کلید خصوصی…» را بزنید (یک‌بار کافی است؛ مسیر به‌خاطر سپرده می‌شود)"
                : "کلید خصوصی بارگذاری نشده");
            return;
        }

        try
        {
            var pem = File.ReadAllText(path);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            _privateKeyPem = pem;
            _privateKeyPath = path;
            _loadedPublicSpki = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            if (auto && !string.Equals(path, Path.Combine(AppContext.BaseDirectory, "private.pem")))
                SaveKeyPath(path);
            SetKeyState(path, null);
        }
        catch (Exception ex)
        {
            _privateKeyPem = null;
            SetKeyState(null, "خواندن کلید خصوصی ممکن نشد — " + ex.Message);
        }
    }

    private void SaveKeyPath(string path)
    {
        try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new Dictionary<string, string> { ["KeyPath"] = path })); }
        catch { }
    }

    private void SetKeyState(string? path, string? error)
    {
        // فاز ۵۶ — هشدار «کلید لازم است» به جای درستِ خودش منتقل شد: داخل کادر «کلید» بالای
        // فرم و کنار دکمهٔ انتخاب؛ دیگر برچسب قرمز کنار دکمهٔ صدور که کاربر را گم می‌کرد وجود ندارد.
        if (error is not null)
        {
            _stKey.Text = "⚠ " + error;
            _btnIssue.Enabled = false;
            _lblKeyState.Text = "⚠ " + error;
            _lblKeyState.ForeColor = Color.Firebrick;
            StyleKeyCta(missing: true);
            _lblIssueResult.Text = "";
            return;
        }
        var shortPath = path is null ? "" : path.Length > 46 ? "…" + path[^46..] : path;
        _stKey.Text = "✓ کلید خصوصی بارگذاری شد: " + shortPath;
        _btnIssue.Enabled = true;
        _lblKeyState.Text = "✓ کلید بارگذاری شد — " + shortPath;
        _lblKeyState.ForeColor = Color.SeaGreen;
        StyleKeyCta(missing: false);
        _lblIssueResult.Text = "";
    }

    /// <summary>فاز ۵۶ — تا وقتی کلیدی انتخاب نشده، دکمهٔ «انتخاب کلید خصوصی» برجستهٔ CTA می‌شود
    /// (سبزآبی پررنگ + توپر) تا تنها راهِ رفع هشدار، از همان اول دیده شود.</summary>
    private void StyleKeyCta(bool missing)
    {
        _btnLoadKey.FlatStyle = FlatStyle.Flat;
        _btnLoadKey.BackColor = missing ? Brand : BrandSoft;
        _btnLoadKey.ForeColor = missing ? Color.White : Color.FromArgb(31, 41, 55);
        _btnLoadKey.Font = Program.Vazir(10f, missing ? FontStyle.Bold : FontStyle.Regular);
    }

    private void PickPrivateKey()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "انتخاب فایل کلید خصوصی (private.pem)",
            Filter = "کلید PEM (*.pem;*.key;*.txt)|*.pem;*.key;*.txt|همهٔ فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var pem = File.ReadAllText(dlg.FileName);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            _privateKeyPem = pem;
            _privateKeyPath = dlg.FileName;
            _loadedPublicSpki = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            SaveKeyPath(dlg.FileName);
            SetKeyState(dlg.FileName, null);
            RefreshHistory();
            if (_wizStep == 1) ShowStep(2);   // نسخهٔ ۱.۶.۰ — ویزارد: با بارگذاری موفق کلید، به گام بعد
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "این فایل کلید خصوصی معتبر نیست:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, RtlMsg);
        }
    }

    // ==================================================================
    // فاز ۵۱ — ساخت جفت‌کلید جدید (RSA-2048) در خود استودیو
    // ==================================================================
    private void GenerateKeyPair()
    {
        var warn = "نکتهٔ مهم: اگر جفت‌کلید عوض شود، مجوزهای قبلی صادرشده با کلید قدیمی دیگر اعتبار ندارند و باید" +
                   "\nرشتهٔ کلید عمومی جدید (PublicKeySpki) در سورس برنامهٔ وب به‌روز و برنامه دوباره منتشر شود." +
                   "\n\nآیا جفت‌کلید جدید ساخته شود؟";
        if (MessageBox.Show(this, warn, "ساخت جفت‌کلید جدید", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, RtlMsg) != DialogResult.Yes)
            return;

        using var dlg = new SaveFileDialog
        {
            Title = "محل ذخیرهٔ کلید خصوصی (private.pem)",
            FileName = "private.pem",
            Filter = "کلید PEM (*.pem)|*.pem",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var rsa = RSA.Create(2048);
            var privPem = rsa.ExportPkcs8PrivateKeyPem();
            var pubPem = rsa.ExportSubjectPublicKeyInfoPem();
            var privPath = dlg.FileName;
            var pubPath = Path.Combine(Path.GetDirectoryName(privPath) ?? ".", "public.pem");

            File.WriteAllText(privPath, privPem);
            File.WriteAllText(pubPath, pubPem);
            try { File.SetUnixFileMode(privPath, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite); } catch { /* ویندوز */ }

            // بارگذاری فوری کلید جدید برای صدور
            _privateKeyPem = privPem;
            _privateKeyPath = privPath;
            _loadedPublicSpki = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            SaveKeyPath(privPath);
            SetKeyState(privPath, null);
            if (_wizStep == 1) ShowStep(2);   // نسخهٔ ۱.۶.۰ — ویزارد: با ساخت موفق جفت‌کلید، به گام بعد

            var spki = _loadedPublicSpki;
            var spkiLines = new List<string>();
            for (var i = 0; i < spki.Length; i += 60)
                spkiLines.Add(spki.Substring(i, Math.Min(60, spki.Length - i)));
            var spkiBlock = string.Join(" +\n        \"", spkiLines);

            var msg = "جفت‌کلید جدید ساخته شد ✓\n\n" +
                      "کلید خصوصی: " + privPath + "\n(محرمانه — هرگز منتشر/ارسال نشود)\n\n" +
                      "کلید عمومی: " + pubPath + "\n\n" +
                      "گام بعدی: رشتهٔ زیر را در LicenseService.cs (PublicKeySpki) و EmbeddedKeys.cs جایگزین و برنامهٔ وب را دوباره بیلد کنید:\n\n" +
                      "\"" + spkiBlock + "\"";
            try { Clipboard.SetText(spki); } catch { }
            MessageBox.Show(this, msg + "\n\n(رشتهٔ کلید عمومی در کلیپ‌بورد هم کپی شد)", "جفت‌کلید جدید ساخته شد", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, RtlMsg);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ساخت جفت‌کلید ممکن نشد:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, RtlMsg);
        }
    }

    // ==================================================================
    // صدور
    // ==================================================================
    private void DoIssue()
    {
        _lblIssueResult.Text = "";
        if (_privateKeyPem is null)
        {
            ShowStep(1);          // نسخهٔ ۱.۶.۰ — ویزارد کاربر را به گام کلید می‌برد
            _btnLoadKey.Focus(); // فاز ۵۶ — توجه کاربر به جای درست (دکمهٔ انتخاب کلید) هدایت می‌شود
            MessageBox.Show(this, "ابتدا فایل کلید خصوصی (private.pem) را انتخاب کنید — دکمهٔ برجستهٔ «انتخاب کلید خصوصی…» در گام ۱ همین تب است",
                "کلید لازم است", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, RtlMsg);
            return;
        }

        var fp = Fingerprint.Normalize(_txtMachine.Text);
        var fpErr = Fingerprint.ValidationError(fp);
        if (fpErr is not null)
        {
            ShowStep(2);   // نسخهٔ ۱.۶.۰ — ویزارد کاربر را به گام مشخصات می‌برد
            _txtMachine.Focus();
            MessageBox.Show(this, fpErr + "\nقالب درست: " + Fingerprint.Pattern + "\nسریع‌ترین راه: «بازکردن فایل درخواست مشتری» یا «چسباندن امضا»", "امضای سرور نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, RtlMsg);
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtLicensee.Text))
        {
            ShowStep(2);   // نسخهٔ ۱.۶.۰ — هم‌سو با اعتبارسنجی گام ۲
            _txtLicensee.Focus();
            MessageBox.Show(this, "نام دارندهٔ مجوز (شرکت/مشتری) را بنویسید — از فایل .hrreq هم خودکار پر می‌شود.",
                "دارندهٔ مجوز لازم است", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, RtlMsg);
            return;
        }

        DateTime? expires = null;
        if (_cmbTerm.SelectedIndex != TermPermanent)
        {
            var (y, m, d) = SelectedJalali();
            expires = JalaliDates.EndOfDayFromJalali(y, m, d);
            if (expires is null)
            {
                MessageBox.Show(this, "تاریخ شمسی انتخابی معتبر نیست", "تاریخ نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, RtlMsg);
                return;
            }
        }

        var draft = new LicenseDraft(
            fp,
            _txtLicensee.Text.Trim(),
            expires,
            _numMax.Value > 0 ? (int)_numMax.Value : null,
            string.IsNullOrWhiteSpace(_txtNotes.Text) ? null : _txtNotes.Text.Trim(),
            LicenseLockMode.FromIndex(_cmbLockMode.SelectedIndex),
            SelectedModules());

        var content = LicenseIssuer.Issue(_privateKeyPem, draft, out var err);
        if (content is null)
        {
            _lblIssueResult.Text = "✗ " + err;
            _lblIssueResult.ForeColor = Color.Firebrick;
            return;
        }

        _txtOut.Text = content;
        _btnSave.Enabled = true;
        _btnCopy.Enabled = true;
        _lblIssueResult.Text = "مجوز صادر شد ✓";
        _lblIssueResult.ForeColor = Color.SeaGreen;

        AppendLog(new LogEntry
        {
            IssuedAt = DateTime.UtcNow,
            Licensee = draft.Licensee,
            Machine = draft.Machine,
            ExpiresAtUtc = draft.ExpiresAtUtc,
            MaxEmployees = draft.MaxEmployees,
            Notes = draft.Notes,
            LockMode = draft.LockMode,
            Modules = draft.Modules,
            Content = content,
        });
        RefreshHistory();
    }

    /// <summary>فاز ۸۲ — کد ماژول‌های تیک‌خورده (هیچ‌کدام = null یعنی همهٔ ماژول‌ها).</summary>
    private string[]? SelectedModules()
    {
        var picked = new List<string>();
        for (var i = 0; i < _clbModules.Items.Count; i++)
            if (_clbModules.GetItemChecked(i))
                picked.Add(LicenseModules.All[i].Code);
        return picked.Count == 0 ? null : picked.ToArray();
    }

    private void DoSave()
    {
        if (_txtOut.Text.Length == 0) return;
        var slug = new string(_txtLicensee.Text.Trim().Where(char.IsLetterOrDigit).Take(20).ToArray());
        if (slug.Length == 0) slug = "license";
        var fp5 = Fingerprint.Normalize(_txtMachine.Text).Replace("-", "")[..5];
        using var dlg = new SaveFileDialog
        {
            Title = "ذخیرهٔ فایل مجوز",
            Filter = "فایل مجوز (*.dat)|*.dat|متن (*.txt)|*.txt",
            FileName = $"license-{slug}-{fp5}.dat",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(dlg.FileName, _txtOut.Text);
        _lblIssueResult.Text = "ذخیره شد: " + Path.GetFileName(dlg.FileName);
        _lblIssueResult.ForeColor = Color.SeaGreen;
    }

    // ==================================================================
    // تاریخچهٔ صدور
    // ==================================================================
    private sealed class LogEntry
    {
        public DateTime IssuedAt { get; set; }
        public string Licensee { get; set; } = "";
        public string Machine { get; set; } = "";
        public DateTime? ExpiresAtUtc { get; set; }
        public int? MaxEmployees { get; set; }
        public string? Notes { get; set; }
        public string? LockMode { get; set; }   // فاز ۷۸ — پیش‌فرض سرور | warn | lock
        public string[]? Modules { get; set; }  // فاز ۸۲ — ماژول‌های مجاز؛ null = همه
        public string? Content { get; set; }
    }

    private void AppendLog(LogEntry e)
    {
        try { File.AppendAllText(LogPath, JsonSerializer.Serialize(e) + Environment.NewLine); }
        catch { }
    }

    private void RefreshHistory()
    {
        if (_lvHistory is null) return;
        _lvHistory.BeginUpdate();
        _lvHistory.Items.Clear();
        try
        {
            if (File.Exists(LogPath))
            {
                foreach (var line in File.ReadLines(LogPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    LogEntry? e = null;
                    try { e = JsonSerializer.Deserialize<LogEntry>(line); } catch { }
                    if (e is null) continue;
                    var item = new ListViewItem(JalaliDates.Fmt(e.IssuedAt)) { Tag = e };
                    item.SubItems.Add(e.Licensee);
                    item.SubItems.Add(e.Machine);
                    item.SubItems.Add(e.ExpiresAtUtc is { } exp ? JalaliDates.FmtLong(exp) : "دائمی");
                    item.SubItems.Add(e.MaxEmployees is int m ? Fingerprint.ToPersianDigits(m.ToString()) : "—");
                    item.SubItems.Add(LicenseLockMode.Display(e.LockMode));
                    item.SubItems.Add(LicenseModules.Display(e.Modules));
                    item.SubItems.Add(e.Notes ?? "");
                    _lvHistory.Items.Add(item);
                }
            }
        }
        catch { }
        _lvHistory.EndUpdate();
    }
}

internal static class DateExt
{
    public static string yyyy_MM_dd(this DateTime d) => d.ToString("yyyy-MM-dd");
}

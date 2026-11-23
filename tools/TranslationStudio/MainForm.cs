using System.Diagnostics;
using System.Text.Json;

namespace TranslationStudio;

/// <summary>
/// پنجرهٔ اصلی استودیو ترجمه — فارسی/راست‌به‌چپ با فونت وزیر (نسخهٔ ۱.۰.۰).
/// خروجی = «بستهٔ ترجمه» (JSON) که با «درون‌ریزی بستهٔ ترجمه» در پنل ادمین سامانه (/Langs)
/// به‌صورت زنده به سامانه اضافه می‌شود — بدون کپی دستی فایل و بدون ری‌استارت.
/// همهٔ کدها و زبان‌ها قابل انتخاب‌اند (کد زبان آزاد است؛ fa رزرو مبدأ است).
/// نسخهٔ ۱.۲.۰ — جدول ترجمه دیگر هرگز خالی باز نمی‌شود: کلیدهای مرجع سامانه (متن‌های
/// مبدأ فارسی + مرجع English) از لحظهٔ باز شدن برنامه پیشِ روست + برچسب وضعیت خالی/فیلتر.
/// </summary>
public partial class MainForm : Form
{
    // ---------- رنگ برند ----------
    private static readonly Color Brand = ColorTranslator.FromHtml("#7c3aed");     // بنفش تیره — متمایز از استودیو مجوز
    private static readonly Color BrandSoft = ColorTranslator.FromHtml("#f5f1fe"); // پس‌زمینهٔ ملایم

    // ---------- وضعیت بسته ----------
    private string _code = "";
    private string _title = "";
    private string _dir = "ltr";
    private string? _path;
    private bool _dirty;
    private Dictionary<string, string> _entries = new(StringComparer.Ordinal);

    // فاز ۷۸b — فهرست مرجع (کلید = متن مبدأ فارسی، مقدار = مرجع انگلیسی) از منبع جاسازی‌شده
    private static Dictionary<string, string>? _ref;

    /// <summary>فهرست مرجع جاسازی‌شده (Resources/ref-en.json — همگام با HrLang.DictEn سرور)؛ غایب/خراب = تهی.</summary>
    private static Dictionary<string, string> Ref
    {
        get
        {
            if (_ref is not null) return _ref;
            _ref = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                using var s = typeof(Program).Assembly.GetManifestResourceStream("TranslationStudio.Resources.ref-en.json");
                if (s is not null)
                {
                    var doc = System.Text.Json.JsonDocument.Parse(s);
                    foreach (var p in doc.RootElement.EnumerateObject())
                        if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                            _ref[p.Name] = p.Value.GetString() ?? "";
                }
            }
            catch { /* مرجع غایب = ستون مرجع خالی — ابزار بدون آن هم کار می‌کند */ }
            return _ref;
        }
    }

    // ---------- کنترل‌ها ----------
    private ComboBox _cmbCode = null!;
    private ComboBox _cmbTitle = null!;
    private ComboBox _cmbDir = null!;
    private DataGridView _grid = null!;
    private Label _lblEmpty = null!;   // نسخهٔ ۱.۲.۰ — برچسب وضعیت جدول خالی (فیلتر بی‌نتیجه یا مرجع خراب)
    private TextBox _txtSearch = null!;
    private CheckBox _chkEmpty = null!;
    private Label _lblStats = null!;
    private ToolStripStatusLabel _lblState = null!;
    private Button _btnSave = null!;

    // کدهای پیشنهادی — قابل ویرایش/افزودن آزاد (هر کد لاتین ۲ تا ۸ نویسه)
    private static readonly (string Code, string Title, string Dir)[] Suggestions =
    {
        ("en", "English", "ltr"), ("ar", "العربية", "rtl"), ("de", "Deutsch", "ltr"),
        ("fr", "Français", "ltr"), ("tr", "Türkçe", "ltr"), ("ru", "Русский", "ltr"),
        ("es", "Español", "ltr"), ("pt", "Português", "ltr"), ("it", "Italiano", "ltr"),
        ("zh", "中文", "ltr"), ("ur", "اردو", "rtl"), ("ps", "پښتو", "rtl"), ("ku", "کوردی", "rtl"),
    };

    private static string AppDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TranslationStudio");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public MainForm()
    {
        Text = "استودیو ترجمه — ساخت بستهٔ زبان برای سامانه";
        Font = Program.Vazir(10f);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(980, 720);
        MinimumSize = new Size(860, 620);
        // نسخهٔ ۱.۲.۱ — اگر محتوا در پنجره‌های کوتاه (DPI بالا) جا نشد، با اسکرول در دسترس بماند
        AutoScroll = true;

        BuildUi();
        SeedReferenceAtStartup();   // نسخهٔ ۱.۲.۰ — جدول از لحظهٔ باز شدن با کلیدهای مرجع سامانه پر است
        UpdateState();
    }

    /// <summary>
    /// نسخهٔ ۱.۲.۰ — رفع «جدول خالی‌اید!»: در باز شدن برنامه، همهٔ کلیدهای مرجع سامانه
    /// (متن‌های مبدأ فارسی) با ترجمهٔ خالی پیشِ مترجم‌اند — کد/نام/جهت زبان را بنویسید و
    /// ترجمه کنید. اگر مرجع جاسازی‌شده در دسترس نباشد، برچسب هشدار در جای جدول دیده می‌شود.
    /// </summary>
    private void SeedReferenceAtStartup()
    {
        if (Ref.Count == 0) return;   // مرجع خراب — RefillGrid برچسب هشدار قرمز را نشان می‌دهد
        foreach (var k in Ref.Keys) _entries[k] = "";
        _dir = "ltr";
        _cmbDir.SelectedItem = "ltr";
        _dirty = false;
        RefillGrid();
    }

    // ==================================================================
    // ساخت رابط کاربری
    // ==================================================================
    private void BuildUi()
    {
        var banner = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Brand };
        var lblTitle = new Label
        {
            Text = "🌐 استودیو ترجمه", ForeColor = Color.White,
            Font = Program.Vazir(14f, FontStyle.Bold), AutoSize = true, Location = new Point(18, 6),
        };
        var lblSub = new Label
        {
            Text = "ساخت بستهٔ ترجمه برای افزودن زبان جدید به سامانه — نسخهٔ " + ToPersianDigits(Program.VersionText),
            ForeColor = Color.FromArgb(231, 224, 250), Font = Program.Vazir(9f), AutoSize = true, Location = new Point(20, 33),
        };
        banner.Controls.AddRange(new Control[] { lblTitle, lblSub });

        var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        root.MinimumSize = new Size(840, 0);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // نسخهٔ ۱.۲.۱ — رفع «ظاهر به‌هم‌ریخته»: ستون باید به عرض پنجره مقید شود، نه عرض ترجیحیِ محتوا
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // ---------- ۱) مشخصات بسته ----------
        var gbInfo = new GroupBox { Text = "۱) مشخصات زبان بسته", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var infoTbl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 6, RowCount = 2 };
        infoTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        infoTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        infoTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        infoTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        infoTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        infoTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        infoTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // کد زبان — کمبوباکس قابل تایپ: همهٔ کدها قابل انتخاب + ورود آزاد (تقاضای کاربر)
        _cmbCode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
        foreach (var s in Suggestions) _cmbCode.Items.Add(s.Code);
        _cmbCode.TextChanged += (_, _) => { _code = _cmbCode.Text.Trim().ToLowerInvariant(); MarkDirty(); };

        // نام نمایشی — هم قابل تایپ، هم پیشنهاد هم‌زمان با کد
        _cmbTitle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
        foreach (var s in Suggestions) _cmbTitle.Items.Add(s.Title);
        _cmbTitle.TextChanged += (_, _) => { _title = _cmbTitle.Text.Trim(); MarkDirty(); };
        _cmbCode.TextChanged += (_, _) =>
        {
            var hit = Suggestions.FirstOrDefault(s => s.Code == _cmbCode.Text.Trim().ToLowerInvariant());
            if (hit.Code is not null && _cmbTitle.Text.Trim().Length == 0)
            {
                _cmbTitle.Text = hit.Title;
                _cmbDir.SelectedItem = hit.Dir;
            }
        };

        _cmbDir = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _cmbDir.Items.AddRange(new object[] { "ltr", "rtl" });
        _cmbDir.SelectedIndexChanged += (_, _) => { if (_cmbDir.SelectedItem is { } d) { _dir = d.ToString()!; MarkDirty(); } };

        infoTbl.Controls.Add(MakeLabel("کد زبان *"), 0, 0);
        infoTbl.Controls.Add(_cmbCode, 1, 0);
        infoTbl.Controls.Add(MakeLabel("نام نمایشی *"), 2, 0);
        infoTbl.Controls.Add(_cmbTitle, 3, 0);
        infoTbl.Controls.Add(MakeLabel("جهت *"), 4, 0);
        infoTbl.Controls.Add(_cmbDir, 5, 0);

        // ردیف دکمه‌های بسته
        var btnNew = new Button { Text = "🆕 بستهٔ جدید", AutoSize = true, MinimumSize = new Size(0, 32) };
        var btnFromKeys = new Button { Text = "🧬 بستهٔ جدید از کلیدهای بستهٔ باز…", AutoSize = true, MinimumSize = new Size(0, 32), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        var btnFillRef = new Button
        {
            Text = "⤵️ پرکردن از مرجع", AutoSize = true, MinimumSize = new Size(0, 32),
            BackColor = BrandSoft, FlatStyle = FlatStyle.Flat,
        };
        var btnOpen = new Button { Text = "📂 بازکردن بسته…", AutoSize = true, MinimumSize = new Size(0, 32), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        var btnMerge = new Button { Text = "🔗 درون‌ریزی ادغامی…", AutoSize = true, MinimumSize = new Size(0, 32), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        _btnSave = new Button { Text = "💾 ذخیرهٔ بسته…", AutoSize = true, MinimumSize = new Size(0, 32), BackColor = Brand, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f, FontStyle.Bold) };
        btnNew.Click += (_, _) => NewPack(fromKeys: false);
        btnFromKeys.Click += (_, _) => OpenForKeys();
        btnFillRef.Click += (_, _) => FillFromRef();
        btnOpen.Click += (_, _) => OpenPack();
        btnMerge.Click += (_, _) => MergePack();
        _btnSave.Click += (_, _) => SavePack();

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Padding = new Padding(0, 8, 0, 0) };
        btnRow.Controls.AddRange(new Control[] { btnNew, btnFromKeys, btnFillRef, btnOpen, btnMerge, _btnSave });

        infoTbl.Controls.Add(btnRow, 0, 1);
        infoTbl.SetColumnSpan(btnRow, 6);
        gbInfo.Controls.Add(infoTbl);

        // ---------- ۲) جدول ترجمه‌ها ----------
        var gbGrid = new GroupBox { Text = "۲) ترجمه‌ها — راست: مبدأ (فارسی) | وسط: مرجع (English) | چپ: ترجمهٔ شما", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var gridTbl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3 };
        gridTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gridTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gridTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // نسخهٔ ۱.۲.۱ — همان مقیدسازی عرض ستون برای جدول ترجمه‌ها
        gridTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var filterRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };
        _txtSearch = new TextBox { Width = 260, PlaceholderText = "جستجو در متن مبدأ یا ترجمه…" };
        _txtSearch.TextChanged += (_, _) => RefillGrid();
        _chkEmpty = new CheckBox { Text = "فقط خالی‌ها", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
        _chkEmpty.CheckedChanged += (_, _) => RefillGrid();
        _lblStats = new Label { Text = "", AutoSize = true, Padding = new Padding(10, 8, 0, 0), Font = Program.Vazir(10f, FontStyle.Bold) };
        filterRow.Controls.AddRange(new Control[] { _txtSearch, _chkEmpty, _lblStats });
        gridTbl.Controls.Add(filterRow, 0, 0);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, ReadOnly = false, RowHeadersVisible = false,
            AutoSize = false, MinimumSize = new Size(0, 380), Height = 380,
            BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
            SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false,
            RightToLeft = RightToLeft.Yes, Font = Program.Vazir(9.5f),
        };
        var cKey = new DataGridViewTextBoxColumn
        {
            Name = "key", HeaderText = "متن مبدأ (فارسی)", ReadOnly = true, FillWeight = 40,
        };
        // فاز ۷۸b — ستون مرجع (انگلیسی) فقط نمایشی: مترجم می‌داند هر رشته چیست و ترجمه‌اش را بنویسد
        var cRef = new DataGridViewTextBoxColumn
        {
            Name = "ref", HeaderText = "مرجع (English)", ReadOnly = true, FillWeight = 32,
            DefaultCellStyle = { ForeColor = Color.DimGray },
        };
        var cVal = new DataGridViewTextBoxColumn
        {
            Name = "val", HeaderText = "ترجمهٔ شما", ReadOnly = false, FillWeight = 28,
        };
        _grid.Columns.AddRange(new DataGridViewColumn[] { cKey, cRef, cVal });
        _grid.CellEndEdit += Grid_CellEndEdit;

        // نسخهٔ ۱.۲.۰ — برچسب وضعیت جدول خالی: دو حالت دارد (مرجع خراب = قرمز؛ فیلتر بی‌نتیجه = خاکستری)
        var gridHost = new Panel { Dock = DockStyle.Fill, MinimumSize = new Size(0, 380), Height = 380, BackColor = Color.White };
        _lblEmpty = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false,
            BackColor = Color.White, Font = Program.Vazir(12f, FontStyle.Bold),
            ForeColor = Color.Firebrick,
        };
        gridHost.Controls.Add(_grid);
        gridHost.Controls.Add(_lblEmpty);
        _lblEmpty.BringToFront();
        gridTbl.Controls.Add(gridHost, 0, 1);

        // نسخهٔ ۱.۲.۱ — رفع «راهنما دیده نمی‌شود»: برچسب AutoSize با متن بلندِ تک‌خطی، عرض ستون را
        // ~۲۶۰۰px متورم می‌کرد و خودش هم از پنجره بیرون می‌زد. حالا عرضش = عرض پنجره (Dock=Top)
        // و ارتفاعش دقیقاً به‌اندازهٔ متنِ شکسته‌شده محاسبه می‌شود.
        var hint = new Label
        {
            Text = "راهنما: جدول از لحظهٔ باز شدن با کلیدهای مرجع سامانه (متن‌های مبدأ فارسی + مرجع English) پر است — کد/نام/جهت زبان را بنویسید، ترجمه کنید و ذخیره کنید؛ «بستهٔ جدید» کلیدها را از نو می‌آورد و «پرکردن از مرجع» ردیف‌های خالی را با متن مرجع انگلیسی پر می‌کند تا ویرایشش سریع‌تر شود. کلیدِ ترجمه‌نشده در سامانه به فارسی (مبدأ) نمایش داده می‌شود — پوشش تدریجی امن است.",
            AutoSize = false, Dock = DockStyle.Top, Height = 64,
            Padding = new Padding(0, 6, 0, 0), ForeColor = Color.DimGray,
        };
        gridTbl.Controls.Add(hint, 0, 2);
        hint.Resize += (_, _) =>   // ارتفاع = ارتفاع واقعی متن در عرض جاری (در هر تغییر اندازه)
        {
            var w = Math.Max(120, hint.ClientSize.Width - hint.Padding.Horizontal);
            var h = TextRenderer.MeasureText(hint.Text, hint.Font, new Size(w, int.MaxValue), TextFormatFlags.WordBreak).Height;
            hint.Height = h + hint.Padding.Vertical + 2;
        };
        gbGrid.Controls.Add(gridTbl);

        // ---------- نوار وضعیت ----------
        var st = new StatusStrip { Font = Program.Vazir(9f) };
        _lblState = new ToolStripStatusLabel("بسته: جدید") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
        var stVer = new ToolStripStatusLabel("نسخهٔ " + ToPersianDigits(Program.VersionText));
        st.Items.Add(_lblState);
        st.Items.Add(new ToolStripStatusLabel("  |  "));
        st.Items.Add(stVer);

        root.Controls.Add(gbInfo, 0, 0);
        root.Controls.Add(gbGrid, 0, 1);
        Controls.Add(root);
        Controls.Add(st);
        Controls.Add(banner);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text, AutoSize = true, Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 8, 8, 4),
    };

    private static string ToPersianDigits(string s) => new(s.Select(c => c is >= '0' and <= '9'
        ? (char)('\u06F0' + (c - '0')) : c).ToArray());

    // ==================================================================
    // منطق بسته
    // ==================================================================
    private void MarkDirty()
    {
        _dirty = true;
        UpdateState();
    }

    private void UpdateState()
    {
        var translated = _entries.Values.Count(v => v.Trim().Length > 0);
        _lblStats.Text = $"ترجمه‌شده: {ToPersianDigits(translated.ToString())} از {ToPersianDigits(_entries.Count.ToString())}";
        var name = string.IsNullOrEmpty(_code) ? "بدون کد" : _code;
        _lblState.Text = $"بسته: {name} {(string.IsNullOrEmpty(_title) ? "" : "— " + _title)} {(string.IsNullOrEmpty(_path) ? "(ذخیره نشده)" : _path)}" + (_dirty ? "  •  تغییرات ذخیره‌نشده" : "");
        _btnSave.Enabled = _entries.Count > 0 || _dirty;
    }

    private void RefillGrid()
    {
        var q = _txtSearch.Text.Trim();
        IEnumerable<KeyValuePair<string, string>> rows = _entries.OrderBy(kv => kv.Key, StringComparer.Ordinal);
        if (_chkEmpty.Checked) rows = rows.Where(kv => kv.Value.Trim().Length == 0);
        if (q.Length > 0)
            rows = rows.Where(kv => kv.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || (Ref.TryGetValue(kv.Key, out var rv) && rv.Contains(q, StringComparison.OrdinalIgnoreCase))
                                    || kv.Value.Contains(q, StringComparison.OrdinalIgnoreCase));
        _grid.Rows.Clear();
        foreach (var (k, v) in rows)
            _grid.Rows.Add(k, Ref.TryGetValue(k, out var refv) ? refv : "", v);   // فاز ۷۸b — ستون مرجع
        // نسخهٔ ۱.۲.۰ — برچسب وضعیت جدول خالی (قرمز = مرجع جاسازی‌شده در دسترس نیست؛ خاکستری = فیلتر بی‌نتیجه)
        var empty = _grid.Rows.Count == 0;
        _lblEmpty.Visible = empty;
        if (empty)
        {
            _lblEmpty.Text = Ref.Count == 0
                ? "⚠ فهرست مرجع سامانه در این نسخه بارگذاری نشد — دکمهٔ «بستهٔ جدید» و «بازکردن بسته…» کار می‌کنند؛ لطفاً این نسخه را گزارش دهید"
                : "موردی مطابق جستجو/فیلتر نیست — جستجو یا «فقط خالی‌ها» را پاک کنید";
            _lblEmpty.ForeColor = Ref.Count == 0 ? Color.Firebrick : Color.DimGray;
        }
        UpdateState();
    }

    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name is not "val") return;   // فقط ستون ترجمه ویرایش‌پذیر است (فاز ۷۸b)
        var key = _grid.Rows[e.RowIndex].Cells["key"].Value?.ToString();
        if (key is null) return;
        var val = _grid.Rows[e.RowIndex].Cells["val"].Value?.ToString() ?? "";
        if (!_entries.ContainsKey(key)) return;   // ستون مبدأ readonly است — تغییرناپذیر
        if (_entries[key] != val)
        {
            _entries[key] = val;
            _dirty = true;
            UpdateState();
        }
    }

    private void NewPack(bool fromKeys)
    {
        if (_dirty && !ConfirmDiscard()) return;
        // فاز ۷۸b — «بستهٔ جدید» دیگر خالی نیست: همهٔ کلیدهای مرجع سامانه (متن‌های مبدأ فارسی)
        // با ستون مرجع (English) از منبع جاسازی‌شده پیش می‌آیند تا مترجم بلافاصله ترجمه کند.
        var keys = fromKeys ? _entries.Keys.ToList() : Ref.Keys.ToList();
        _entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var k in keys) _entries[k] = "";
        _code = ""; _title = ""; _dir = "ltr"; _path = null; _dirty = keys.Count > 0;
        _cmbCode.Text = ""; _cmbTitle.Text = ""; _cmbDir.SelectedItem = "ltr";
        RefillGrid();
        UpdateState();
        if (!fromKeys && keys.Count > 0)
            MessageBox.Show(this,
                $"{ToPersianDigits(keys.Count.ToString())} کلید مرجع سامانه بارگذاری شد — ستون مرجع (English) برای راهنمایی مترجم پیشِ روست.",
                "بستهٔ جدید", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// فاز ۷۸b — پرکردن ردیف‌های خالی از مرجع انگلیسی — شروع سریع ترجمه:
    /// مترجم به‌جای تایپ از صفر، مرجع هر ردیف را ویرایش می‌کند.
    /// </summary>
    private void FillFromRef()
    {
        var filled = 0;
        foreach (var k in _entries.Keys.ToList())
        {
            if (_entries[k].Trim().Length > 0) continue;
            if (Ref.TryGetValue(k, out var v) && v.Trim().Length > 0) { _entries[k] = v; filled++; }
        }
        _dirty = _dirty || filled > 0;
        RefillGrid();
        UpdateState();
        MessageBox.Show(this,
            $"{ToPersianDigits(filled.ToString())} ردیف خالی با مرجع انگلیسی پر شد — حالا ویرایشش کنید.",
            "پرکردن از مرجع", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenForKeys()
    {
        if (!TryOpenJson(out var code, out var title, out var dir, out var loaded)) return;
        if (_dirty && !ConfirmDiscard()) return;
        _entries = loaded.Keys.ToDictionary(k => k, _ => "", StringComparer.Ordinal);
        _code = ""; _title = ""; _dir = "ltr"; _path = null; _dirty = _entries.Count > 0;
        _cmbCode.Text = ""; _cmbTitle.Text = ""; _cmbDir.SelectedItem = "ltr";
        RefillGrid();
        UpdateState();
        MessageBox.Show(this,
            $"{ToPersianDigits(loaded.Count.ToString())} کلید مبدأ گرفته شد و ترجمه‌ها خالی شدند.\nحالا کد/نام/جهت زبان جدید را بنویسید و ترجمه کنید.",
            "بستهٔ جدید از کلیدها", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenPack()
    {
        if (_dirty && !ConfirmDiscard()) return;
        if (!TryOpenJson(out var code, out var title, out var dir, out var loaded)) return;
        _entries = loaded;
        _code = code; _title = title; _dir = dir;
        _cmbCode.Text = code; _cmbTitle.Text = title; _cmbDir.SelectedItem = dir is "rtl" ? "rtl" : "ltr";
        _dirty = false;
        RefillGrid();
        UpdateState();
    }

    private void MergePack()
    {
        if (!TryOpenJson(out _, out _, out _, out var loaded)) return;
        int updated = 0, added = 0;
        foreach (var (k, v) in loaded)
        {
            if (_entries.ContainsKey(k))
            {
                if (v.Trim().Length > 0 && _entries[k] != v) { _entries[k] = v; updated++; }
            }
            else
            {
                _entries[k] = v;
                added++;
            }
        }
        _dirty = updated + added > 0;
        RefillGrid();
        UpdateState();
        MessageBox.Show(this,
            $"ادغام انجام شد — {ToPersianDigits(added.ToString())} کلید جدید، {ToPersianDigits(updated.ToString())} به‌روزرسانی.",
            "درون‌ریزی ادغامی", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SavePack()
    {
        var code = _code.Trim().ToLowerInvariant();
        if (code.Length is < 2 or > 8 || !code.All(char.IsAsciiLetterOrDigit))
        {
            MessageBox.Show(this, "کد زبان باید ۲ تا ۸ نویسهٔ لاتین باشد (مثل de، ar، tr).", "کد نامعتبر",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (code == "fa")
        {
            MessageBox.Show(this, "«fa» زبان مبدأ است — بستهٔ ترجمه نمی‌پذیرد. برای ترجمهٔ en یا زبان تازه، کد دیگری بگذارید.", "کد رزرو",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_title.Trim().Length < 2)
        {
            MessageBox.Show(this, "نام نمایشی زبان را بنویسید (مثل Deutsch).", "نام نمایشی لازم است",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Title = "ذخیرهٔ بستهٔ ترجمه",
            Filter = "بستهٔ ترجمه (*.json)|*.json|همهٔ فایل‌ها (*.*)|*.*",
            FileName = $"hr-lang-{code}.json",
            InitialDirectory = Directory.Exists(AppDir) ? AppDir : null,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var pack = new
        {
            format = "hr-lang-pack",
            version = 1,
            code,
            title = _title.Trim(),
            dir = _dir is "rtl" ? "rtl" : "ltr",
            entries = _entries,
        };
        try
        {
            var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            _path = dlg.FileName;
            _dirty = false;
            UpdateState();
            _lblState.Text += "  ✓";
            MessageBox.Show(this,
                "بستهٔ ترجمه ذخیره شد.\n\nگام بعدی: در سامانه، پنل ادمین ← «زبان‌ها و ترجمه» ← «درون‌ریزی بستهٔ ترجمه» — این فایل را بدهید؛ زبان بلافاصله در سوئیچر زبان ظاهر می‌شود (بدون ری‌استارت).",
                "ذخیره شد", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ذخیره ممکن نشد — " + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TryOpenJson(out string code, out string title, out string dir, out Dictionary<string, string> entries)
    {
        code = ""; title = ""; dir = "ltr"; entries = new(StringComparer.Ordinal);
        using var dlg = new OpenFileDialog
        {
            Title = "بازکردن بستهٔ ترجمه",
            Filter = "بستهٔ ترجمه (*.json)|*.json|همهٔ فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(dlg.FileName));
            var r = doc.RootElement;
            code = r.TryGetProperty("code", out var c) ? (c.GetString() ?? "") : "";
            title = r.TryGetProperty("title", out var t) ? (t.GetString() ?? "") : "";
            dir = r.TryGetProperty("dir", out var d) ? (d.GetString() ?? "ltr") : "ltr";
            entries = new Dictionary<string, string>(StringComparer.Ordinal);
            if (r.TryGetProperty("entries", out var en) && en.ValueKind == JsonValueKind.Object)
                foreach (var p in en.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String)
                        entries[p.Name] = p.Value.GetString() ?? "";
            if (entries.Count == 0)
            {
                var ok = MessageBox.Show(this,
                    "این بسته هیچ ترجمه‌ای ندارد (احتمالاً بستهٔ مبدأ fa است). کلیدهای آن به‌عنوان فهرست مبدأ باز شود؟",
                    "بستهٔ خالی", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ok != DialogResult.Yes) return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "بسته معتبر نیست — " + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool ConfirmDiscard()
    {
        var r = MessageBox.Show(this, "تغییرات ذخیره‌نشده دور ریخته شود؟", "تغییرات ذخیره نشده",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        return r == DialogResult.Yes;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_dirty)
        {
            var r = MessageBox.Show(this, "تغییرات ذخیره نشده‌اند — بدون ذخیره خارج شویم؟", "خروج",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r == DialogResult.No) { e.Cancel = true; return; }
        }
        base.OnFormClosing(e);
    }
}

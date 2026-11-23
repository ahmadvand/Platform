using System.Text.Json;

namespace HrTranslationStudio;

/// <summary>
/// پنجرهٔ اصلی استودیو ترجمه — فارسی/راست‌به‌چپ با فونت وزیر.
/// گار: واژه‌نامهٔ مرجع (فارسی→انگلیسی محصول) → شبکهٔ ترجمهٔ زبان هدف → بستهٔ زبان JSON.
/// بستهٔ خروجی در App_Data/i18n نصب می‌شود و بدون ری‌استارت در سوییچر 🌐 ظاهر می‌گردد.
/// </summary>
public class MainForm : Form
{
    // ---------- رنگ برند (هم‌خانوادهٔ استودیو مجوز) ----------
    private static readonly Color Brand = ColorTranslator.FromHtml("#0f766e");
    private static readonly Color BrandSoft = ColorTranslator.FromHtml("#eef7f6");

    // ---------- مدل ----------
    private sealed class RefRow
    {
        public string Key { get; init; } = "";       // متن مبدأ فارسی (کلید)
        public string Ref { get; init; } = "";       // مرجع انگلیسی (اگر در واژه‌نامهٔ مرجع بود)
        public string Target { get; set; } = "";     // ترجمهٔ هدف
    }

    private List<RefRow> _rows = [];
    private Dictionary<string, string> _reference = new(StringComparer.Ordinal);
    private string _referencePath = "";
    private string? _packPath;                       // بستهٔ بازشده (برای «ذخیره» روی همان)

    // ---------- کنترل‌ها ----------
    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private TextBox _txtCode = null!, _txtName = null!, _txtAuthor = null!, _txtVersion = null!;
    private ComboBox _cmbDir = null!;
    private ToolStripStatusLabel _lblStatus = null!, _lblProgress = null!;
    private Button _btnSave = null!;

    private static string AppDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HrTranslationStudio");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public MainForm()
    {
        Text = "استودیو ترجمه — سامانه یکپارچه منابع انسانی";
        Font = Program.Vazir(10f);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(940, 640);

        BuildUi();
        TryLoadReference(auto: true);
    }

    // ==================================================================
    // ساخت رابط
    // ==================================================================
    private void BuildUi()
    {
        // نوار وضعیت — زودتر ساخته می‌شود تا شمارندهٔ پیشرفت هم داخلش بنشیند
        var st = new StatusStrip { Font = Program.Vazir(9f) };
        _lblStatus = new ToolStripStatusLabel("واژه‌نامهٔ مرجع بارگذاری نشده — «بستهٔ جدید…» یا «بازکردن بستهٔ ترجمه…» را بزنید")
        { Spring = true, TextAlign = ContentAlignment.MiddleRight };
        st.Items.Add(_lblStatus);
        var banner = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Brand };
        var lblTitle = new Label
        {
            Text = "🌐 استودیو ترجمه", ForeColor = Color.White,
            Font = Program.Vazir(14f, FontStyle.Bold), AutoSize = true, Location = new Point(18, 6),
        };
        var lblSub = new Label
        {
            Text = "تولید بستهٔ ترجمه برای زبان‌های مختلف — خروجی را در پوشهٔ App_Data/i18n نصب سامانه کپی کنید — نسخهٔ " + FaNum.ToPersianDigits(Program.VersionText),
            ForeColor = Color.FromArgb(213, 237, 235), Font = Program.Vazir(9f), AutoSize = true, Location = new Point(20, 33),
        };
        banner.Controls.AddRange(new Control[] { lblTitle, lblSub });

        // ---------- نوار ابزار ----------
        var tools = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 8, 10, 8), BackColor = Color.White,
        };
        var btnNew = new Button { Text = "بستهٔ جدید…", AutoSize = true, MinimumSize = new Size(0, 34), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        var btnOpenPack = new Button { Text = "بازکردن بستهٔ ترجمه…", AutoSize = true, MinimumSize = new Size(0, 34), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        var btnOpenRef = new Button { Text = "واژه‌نامهٔ مرجع (فارسی←انگلیسی)…", AutoSize = true, MinimumSize = new Size(0, 34), BackColor = BrandSoft, FlatStyle = FlatStyle.Flat };
        _btnSave = new Button { Text = "💾 ذخیرهٔ بسته…", AutoSize = true, MinimumSize = new Size(0, 34), BackColor = Brand, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Program.Vazir(10f, FontStyle.Bold), Enabled = false };
        btnNew.Click += (_, _) => NewPack();
        btnOpenPack.Click += (_, _) => OpenPack();
        btnOpenRef.Click += (_, _) => TryLoadReference(auto: false);
        _btnSave.Click += (_, _) => SavePack();
        tools.Controls.AddRange(new Control[] { _btnSave, btnNew, btnOpenPack, btnOpenRef });

        // ---------- متادیتا + جستجو ----------
        var meta = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 5, RowCount = 1,
            Padding = new Padding(10, 0, 10, 6), BackColor = Color.White,
        };
        for (var i = 0; i < 5; i++)
            meta.ColumnStyles.Add(i == 4 ? new ColumnStyle(SizeType.Percent, 100) : new ColumnStyle(SizeType.AutoSize));

        _txtCode = new TextBox { Width = 90, RightToLeft = RightToLeft.No, Font = new Font("Consolas", 10.5f), PlaceholderText = "کد زبان" };
        _txtName = new TextBox { Width = 150, PlaceholderText = "نام نمایشی (به خودِ زبان)" };
        _cmbDir = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        _cmbDir.Items.AddRange(["rtl", "ltr"]);
        _cmbDir.SelectedIndex = 0;
        _txtVersion = new TextBox { Width = 80, RightToLeft = RightToLeft.No, Text = "1.0.0" };
        _txtAuthor = new TextBox { Width = 160, PlaceholderText = "مترجم/سازمان", Dock = DockStyle.Fill };
        meta.Controls.Add(Wrap("کد", _txtCode), 0, 0);
        meta.Controls.Add(Wrap("نام", _txtName), 1, 0);
        meta.Controls.Add(Wrap("جهت", _cmbDir), 2, 0);
        meta.Controls.Add(Wrap("نسخه", _txtVersion), 3, 0);
        meta.Controls.Add(Wrap("مترجم", _txtAuthor), 4, 0);

        var searchRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10, 2, 10, 6), BackColor = Color.White, WrapContents = false,
        };
        _txtSearch = new TextBox { Width = 420, PlaceholderText = "🔍 جستجو در متن فارسی، مرجع انگلیسی یا ترجمه…" };
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        _lblProgress = new ToolStripStatusLabel
        {
            Text = "", Font = Program.Vazir(10f, FontStyle.Bold),
            ForeColor = Color.SeaGreen,
        };
        searchRow.Controls.Add(_txtSearch);
        st.Items.Add(_lblProgress);

        // ---------- شبکهٔ ترجمه ----------
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White, BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(226, 230, 238),
            EnableHeadersVisualStyles = false, Font = Program.Vazir(10f),
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 243, 252);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(29, 30, 58);
        _grid.ColumnHeadersDefaultCellStyle.Font = Program.Vazir(10f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(242, 243, 252);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 247, 246);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(17, 24, 39);
        _grid.RowHeadersWidth = 24;

        var cKey = new DataGridViewTextBoxColumn
        {
            Name = "key", HeaderText = "متن مبدأ (فارسی) — کلید", FillWeight = 40, ReadOnly = true,
            DefaultCellStyle = { WrapMode = DataGridViewTriState.True },
        };
        var cRef = new DataGridViewTextBoxColumn
        {
            Name = "ref", HeaderText = "مرجع انگلیسی", FillWeight = 27, ReadOnly = true,
            DefaultCellStyle = { WrapMode = DataGridViewTriState.True },
        };
        var cTarget = new DataGridViewTextBoxColumn
        {
            Name = "target", HeaderText = "ترجمهٔ زبان هدف", FillWeight = 33,
            DefaultCellStyle = { WrapMode = DataGridViewTriState.True },
        };
        _grid.Columns.AddRange([cKey, cRef, cTarget]);
        _grid.CellEndEdit += (_, _) => { SyncTargetFromGrid(); UpdateProgress(); };
        _grid.CellValueChanged += (_, _) => { UpdateProgress(); };

        // ---------- نوار وضعیت پایین ----------
        Controls.Add(_grid);
        Controls.Add(searchRow);
        Controls.Add(meta);
        Controls.Add(tools);
        Controls.Add(st);
        Controls.Add(banner); // ترتیب Dock: بنر بالا، ابزار/متا/جستجو، شبکه، وضعیت پایین
    }

    private static Panel Wrap(string caption, Control c)
    {
        var p = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(2, 0, 10, 0) };
        p.Controls.Add(new Label { Text = caption, AutoSize = true, Font = Program.Vazir(8.8f), ForeColor = Color.DimGray, Margin = new Padding(0) });
        p.Controls.Add(c);
        return p;
    }

    // ==================================================================
    // واژه‌نامهٔ مرجع
    // ==================================================================
    private void TryLoadReference(bool auto)
    {
        // ۱) مسیر ذخیره‌شدهٔ قبلی ۲) کنار فایل اجرایی (base-i18n-reference.json) ۳) انتخابگر فایل
        string? path = null;
        try
        {
            if (File.Exists(ConfigPath))
            {
                var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ConfigPath));
                if (cfg is { } && cfg.TryGetValue("RefPath", out var p) && File.Exists(p)) path = p;
            }
            var beside = Path.Combine(AppContext.BaseDirectory, "base-i18n-reference.json");
            if (path is null && File.Exists(beside)) path = beside;
            if (!auto && path is null)
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "انتخاب واژه‌نامهٔ مرجع (فارسی←انگلیسی محصول)",
                    Filter = "JSON (*.json)|*.json|همهٔ فایل‌ها (*.*)|*.*",
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                path = dlg.FileName;
            }
        }
        catch { }

        if (path is null)
        {
            _lblStatus.Text = auto
                ? "واژه‌نامهٔ مرجع کنار برنامه پیدا نشد — با «بازکردن بستهٔ ترجمه…» شروع کنید یا فایل مرجع را کنار exe بگذارید"
                : "واژه‌نامهٔ مرجع بارگذاری نشد";
            return;
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path),
                new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip });
            if (dict is null || dict.Count == 0)
            {
                _lblStatus.Text = "واژه‌نامهٔ مرجع خوانده شد اما خالی است";
                return;
            }
            _reference = dict;
            _referencePath = path;
            SaveRefPath(path);
            // ردیف‌ها را با مرجع تازه همگام کن (ترجمه‌های موجود حفظ می‌شوند)
            var targets = _rows.ToDictionary(r => r.Key, r => r.Target, StringComparer.Ordinal);
            _rows = dict.Keys.Select(k => new RefRow { Key = k, Ref = dict[k], Target = targets.GetValueOrDefault(k, "") }).ToList();
            _btnSave.Enabled = _rows.Count > 0;
            BindGrid();
            UpdateProgress();
            _lblStatus.Text = $"واژه‌نامهٔ مرجع: {FaNum.ToPersianDigits(dict.Count.ToString())} رشته — " +
                              Path.GetFileName(path) + " — اکنون «بستهٔ جدید…» یا «بازکردن بستهٔ ترجمه…»";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "خواندن واژه‌نامهٔ مرجع ممکن نشد:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string ConfigPath => Path.Combine(AppDir, "config.json");
    private static string LastPackPath => Path.Combine(AppDir, "last-pack.txt");

    private static void SaveRefPath(string path)
    {
        try
        {
            var cfg = File.Exists(ConfigPath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ConfigPath)) ?? []
                : [];
            cfg["RefPath"] = path;
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg));
        }
        catch { }
    }

    // ==================================================================
    // بستهٔ جدید / بازکردن
    // ==================================================================
    private void NewPack()
    {
        if (_rows.Count == 0)
        {
            MessageBox.Show(this, "ابتدا واژه‌نامهٔ مرجع باید حاضر باشد — «واژه‌نامهٔ مرجع…» را بزنید.", "مرجع لازم است",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var f = new NewPackForm();
        if (f.ShowDialog(this) != DialogResult.OK) return;
        _txtCode.Text = f.PackCode;
        _txtName.Text = f.PackName;
        _cmbDir.SelectedIndex = f.PackDir == "ltr" ? 1 : 0;
        _txtVersion.Text = "1.0.0";
        _packPath = null;
        foreach (var r in _rows) r.Target = "";
        BindGrid();
        UpdateProgress();
        _btnSave.Enabled = true;
        _lblStatus.Text = $"بستهٔ جدید برای «{f.PackName}» آماده است — ترجمه‌ها را در ستون هدف وارد و ذخیره کنید";
    }

    private void OpenPack()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "بازکردن بستهٔ ترجمه (hr-i18n-*.json)",
            Filter = "بستهٔ ترجمه (*.json)|*.json|همهٔ فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(dlg.FileName));
            var root = doc.RootElement;
            string code = "", name = "", dir = "rtl", ver = "1.0.0", author = "";
            if (root.TryGetProperty("meta", out var meta))
            {
                if (meta.TryGetProperty("code", out var v)) code = v.GetString() ?? "";
                if (meta.TryGetProperty("name", out v)) name = v.GetString() ?? "";
                if (meta.TryGetProperty("dir", out v)) dir = v.GetString() ?? "rtl";
                if (meta.TryGetProperty("version", out v)) ver = v.GetString() ?? "1.0.0";
                if (meta.TryGetProperty("author", out v)) author = v.GetString() ?? "";
            }
            // قالب تخت (واژه‌نامهٔ ساده) هم پذیرفته می‌شود
            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("strings", out var st) && st.ValueKind == JsonValueKind.Object)
                foreach (var p in st.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String) strings[p.Name] = p.Value.GetString() ?? "";
            if (strings.Count == 0 && root.ValueKind == JsonValueKind.Object)
                foreach (var p in root.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String) strings[p.Name] = p.Value.GetString() ?? "";
            if (strings.Count == 0)
            {
                MessageBox.Show(this, "در این فایل هیچ رشتهٔ ترجمه‌ای پیدا نشد.", "بستهٔ نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // مرجع در صورت نیاز بارگذاری شود
            if (_rows.Count == 0) TryLoadReference(auto: true);

            var targets = _rows.ToDictionary(r => r.Key, r => r.Target, StringComparer.Ordinal);
            foreach (var kv in strings) targets[kv.Key] = kv.Value;
            _rows = (_rows.Count > 0
                    ? _rows.Select(r => r.Key) // مرجع حاضر: ترتیب/کلیدهای مرجع
                    : strings.Keys)            // بدون مرجع: کلیدهای خود بسته
                .Select(k => new RefRow { Key = k, Ref = _reference.GetValueOrDefault(k, ""), Target = targets.GetValueOrDefault(k, "") })
                .ToList();
            _packPath = dlg.FileName;
            _txtCode.Text = string.IsNullOrEmpty(code) ? SuggestCode(dlg.FileName) : code;
            _txtName.Text = name;
            _cmbDir.SelectedIndex = dir == "ltr" ? 1 : 0;
            _txtVersion.Text = ver;
            _txtAuthor.Text = author;
            _btnSave.Enabled = _rows.Count > 0;
            BindGrid();
            UpdateProgress();
            _lblStatus.Text = $"بستهٔ «{name}» باز شد — {FaNum.ToPersianDigits(strings.Count.ToString())} رشته — ذخیره روی: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "خواندن بسته ممکن نشد:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string SuggestCode(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var m = System.Text.RegularExpressions.Regex.Match(name, @"([a-z]{2,10})$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : "";
    }

    // ==================================================================
    // ذخیرهٔ بسته
    // ==================================================================
    private void SavePack()
    {
        var code = _txtCode.Text.Trim().ToLowerInvariant();
        var name = _txtName.Text.Trim();
        if (code.Length is < 2 or > 10 || !code.All(char.IsAsciiLetterOrDigit))
        {
            MessageBox.Show(this, "کد زبان باید ۲ تا ۱۰ حرف لاتین/عدد باشد (مثل: ar, de, tr).", "کد نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtCode.Focus();
            return;
        }
        if (name.Length == 0) name = code;
        if (code is "fa" or "en")
        {
            MessageBox.Show(this, "کد fa و en رزرو شده‌اند — برای زبان جدید کد دیگری انتخاب کنید.", "کد رزرو", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var dir = (string?)_cmbDir.SelectedItem ?? "rtl";
        var strings = _rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Target))
            .GroupBy(r => r.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Target.Trim(), StringComparer.Ordinal);
        if (strings.Count == 0)
        {
            MessageBox.Show(this, "هنوز هیچ ترجمه‌ای وارد نشده است.", "بستهٔ خالی", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var pack = new Dictionary<string, object?>
        {
            ["meta"] = new Dictionary<string, object?>
            {
                ["code"] = code,
                ["name"] = name,
                ["dir"] = dir,
                ["version"] = string.IsNullOrWhiteSpace(_txtVersion.Text) ? "1.0.0" : _txtVersion.Text.Trim(),
                ["author"] = _txtAuthor.Text.Trim(),
                ["generatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                ["stringsCount"] = strings.Count,
            },
            ["strings"] = strings,
        };

        string path;
        if (_packPath is not null)
        {
            path = _packPath;
        }
        else
        {
            using var dlg = new SaveFileDialog
            {
                Title = "ذخیرهٔ بستهٔ ترجمه",
                Filter = "بستهٔ ترجمه (*.json)|*.json",
                FileName = $"hr-i18n-{code}.json",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            path = dlg.FileName;
        }

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(pack, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
            _packPath = path;
            try { File.WriteAllText(LastPackPath, path); } catch { }
            var total = _rows.Count;
            var done = strings.Count;
            var pct = total == 0 ? 100 : (int)Math.Round(done * 100.0 / total);
            _lblStatus.Text = $"ذخیره شد: {path} — برای نصب، همین فایل را در پوشهٔ App_Data/i18n سامانه کپی کنید";
            MessageBox.Show(this,
                "بستهٔ ترجمه ذخیره شد ✓\n\n" +
                $"زبان: {name} ({code}) — جهت: {dir}\n" +
                $"ترجمه‌شده: {FaNum.ToPersianDigits(done.ToString())} از {FaNum.ToPersianDigits(total.ToString())} ({FaNum.ToPersianDigits(pct.ToString())}٪)\n\n" +
                "نصب: فایل را در پوشهٔ App_Data/i18n سامانه کپی کنید؛\n" +
                "زبان بلافاصله (بدون ری‌استارت) در سوییچر 🌐 و تنظیمات ظاهر می‌شود.",
                "بستهٔ ترجمه", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ذخیره ممکن نشد:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ==================================================================
    // شبکه و فیلتر
    // ==================================================================
    private void BindGrid()
    {
        _grid.Rows.Clear();
        foreach (var r in _rows)
            _grid.Rows.Add(r.Key, r.Ref, r.Target);
    }

    private void ApplyFilter()
    {
        var q = _txtSearch.Text.Trim();
        _grid.Rows.Clear();
        foreach (var r in _rows)
        {
            if (q.Length > 0 &&
                !r.Key.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !r.Ref.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !r.Target.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;
            _grid.Rows.Add(r.Key, r.Ref, r.Target);
        }
    }

    private void SyncTargetFromGrid()
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            var key = Convert.ToString(row.Cells["key"].Value) ?? "";
            var target = Convert.ToString(row.Cells["target"].Value) ?? "";
            var r = _rows.FirstOrDefault(x => x.Key == key);
            if (r is not null) r.Target = target;
        }
    }

    private void UpdateProgress()
    {
        SyncTargetFromGrid();
        var total = _rows.Count;
        var done = _rows.Count(r => !string.IsNullOrWhiteSpace(r.Target));
        var pct = total == 0 ? 0 : (int)Math.Round(done * 100.0 / total);
        _lblProgress.Text = $"ترجمه‌شده: {FaNum.ToPersianDigits(done.ToString())} / {FaNum.ToPersianDigits(total.ToString())} ({FaNum.ToPersianDigits(pct.ToString())}٪)";
        _lblProgress.ForeColor = pct >= 100 ? Color.SeaGreen : pct > 0 ? Color.DarkGoldenrod : Color.DimGray;
    }
}

/// <summary>دیالوگ «بستهٔ جدید» — کد زبان، نام نمایشی و جهت متن.</summary>
public class NewPackForm : Form
{
    public string PackCode { get; private set; } = "";
    public string PackName { get; private set; } = "";
    public string PackDir { get; private set; } = "rtl";

    private readonly TextBox _code = new() { Width = 140, RightToLeft = RightToLeft.No, Font = new Font("Consolas", 11f) };
    private readonly TextBox _name = new() { Width = 220 };
    private readonly ComboBox _dir = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };

    public NewPackForm()
    {
        Text = "بستهٔ ترجمهٔ جدید";
        Font = Program.Vazir(10f);
        RightToLeft = RightToLeft.Yes;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 190);

        _dir.Items.AddRange(["rtl", "ltr"]);
        _dir.SelectedIndex = 0;

        var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(14) };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tbl.Controls.Add(new Label { Text = "کد زبان (لاتین) *", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        tbl.Controls.Add(_code, 1, 0);
        tbl.Controls.Add(new Label { Text = "نام نمایشی (به خودِ زبان) *", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        tbl.Controls.Add(_name, 1, 1);
        tbl.Controls.Add(new Label { Text = "جهت متن", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        tbl.Controls.Add(_dir, 1, 2);
        var hint = new Label
        {
            Text = "مثال: کد ar — نام العربية — جهت rtl\nکدهای fa و en رزرو شده‌اند.",
            AutoSize = true, ForeColor = Color.DimGray, Font = Program.Vazir(9f),
        };
        tbl.Controls.Add(hint, 1, 3);

        var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 6, 14, 10) };
        var ok = new Button { Text = "ایجاد بسته", BackColor = ColorTranslator.FromHtml("#0f766e"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(130, 34), Font = Program.Vazir(10f, FontStyle.Bold) };
        var cancel = new Button { Text = "انصراف", Size = new Size(110, 34), FlatStyle = FlatStyle.Flat };
        ok.Click += (_, _) =>
        {
            var c = _code.Text.Trim().ToLowerInvariant();
            if (c.Length is < 2 or > 10 || !c.All(char.IsAsciiLetterOrDigit) || c is "fa" or "en")
            {
                MessageBox.Show(this, "کد زبان باید ۲ تا ۱۰ حرف لاتین/عدد و غیر از fa/en باشد.", "کد نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_name.Text.Trim().Length == 0)
            {
                MessageBox.Show(this, "نام نمایشی زبان را وارد کنید.", "نام لازم است", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            PackCode = c;
            PackName = _name.Text.Trim();
            PackDir = (string?)_dir.SelectedItem ?? "rtl";
            DialogResult = DialogResult.OK;
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        btns.Controls.Add(ok);
        btns.Controls.Add(cancel);

        Controls.Add(tbl);
        Controls.Add(btns);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

# -*- coding: utf-8 -*-
# فاز ۷۹ — پچ TranslationStudio/MainForm.cs: پیش‌پر کردن هنگام باز شدن + بارگذار مقاوم مرجع
import io, re, sys

P = "/home/z/my-project/tools/TranslationStudio/MainForm.cs"
data = io.open(P, encoding="utf-8").read()

def cut(s, start_anchor, end_anchor, occurrence=1):
    """برش متن میان دو لنگر — لنگر آغاز تا ابتدای لنگر پایان."""
    i = s.find(start_anchor)
    assert i >= 0, "start anchor not found: " + start_anchor[:40]
    j = s.find(end_anchor, i)
    assert j >= 0, "end anchor not found: " + end_anchor[:40]
    return i, j

# ---------- ۱) نسخه در کامنت راهنما ----------
n = data.count("(نسخهٔ ۱.۰.۰)")
assert n == 1, f"version-comment occurrences={n}"
data = data.replace("(نسخهٔ ۱.۰.۰)", "(نسخهٔ ۱.۱.۱ — فاز ۷۹)")

# ---------- ۲) بلوک Ref → بارگذار مقاوم ----------
i, j = cut(data, "    private static Dictionary<string, string>? _ref;", "    // ---------- کنترل‌ها ----------")
new_ref = '''    private static Dictionary<string, string>? _ref;

    /// <summary>فاز ۷۹ — منشأ فهرست مرجع (embedded/file/none) برای پیام خطای صریح به‌جای خالی خاموش.</summary>
    public static string RefSource { get; private set; } = "none";

    /// <summary>فهرست مرجع (Resources/ref-en.json — همگام با HrLang.DictEn سرور)؛ فاز ۷۹: بارگذار مقاوم.</summary>
    private static Dictionary<string, string> Ref
    {
        get
        {
            if (_ref is not null) return _ref;
            _ref = LoadRef();
            return _ref;
        }
    }

    /// <summary>فاز ۷۹ — بارگذاری مقاوم مرجع: ۱) منبع جاسازی‌شده با نام دقیق ۲) هر منبعی که به
    /// ref-en.json ختم شود ۳) فایل ref-en.json کنار فایل اجرایی. خرابی دیگر «خالی خاموش» نیست.</summary>
    private static Dictionary<string, string> LoadRef()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var s = typeof(Program).Assembly.GetManifestResourceStream("TranslationStudio.Resources.ref-en.json");
            if (s is null)
            {
                var alt = typeof(Program).Assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("ref-en.json", StringComparison.OrdinalIgnoreCase));
                if (alt is not null) s = typeof(Program).Assembly.GetManifestResourceStream(alt);
            }
            if (s is not null)
            {
                using (s) FillRef(dict, s);
                if (dict.Count > 0) RefSource = "embedded";
            }
        }
        catch { }

        if (dict.Count == 0)
        {
            try
            {
                var beside = Path.Combine(AppContext.BaseDirectory, "ref-en.json");
                if (File.Exists(beside))
                {
                    using var fs = File.OpenRead(beside);
                    FillRef(dict, fs);
                    if (dict.Count > 0) RefSource = "file";
                }
            }
            catch { }
        }
        return dict;
    }

    private static void FillRef(Dictionary<string, string> dict, Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        var bytes = ms.ToArray();
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            bytes = bytes[3..];   // پذیرش BOM احتمالی
        using var doc = System.Text.Json.JsonDocument.Parse(bytes);
        foreach (var p in doc.RootElement.EnumerateObject())
            if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                dict[p.Name] = p.Value.GetString() ?? "";
    }

'''
data = data[:i] + new_ref + data[j:]

# ---------- ۳) فیلد _lblHint ----------
anchor = "    private Button _btnSave = null!;"
n = data.count(anchor); assert n == 1, f"btnSave anchor={n}"
data = data.replace(anchor, anchor + "\n    private Label _lblHint = null!;   // فاز ۷۹ — هشدار صریحِ نبودن مرجع")

# ---------- ۴) فراخوانی LoadRefEntries در سازنده ----------
ctor_anchor = "        BuildUi();\n        UpdateState();\n    }"
n = data.count(ctor_anchor); assert n == 1, f"ctor anchor={n}"
data = data.replace(ctor_anchor,
    "        BuildUi();\n        // فاز ۷۹ — از همان باز شدن برنامه، جدول با همهٔ کلیدهای مرجع سامانه پیش می‌آید؛\n"
    "        // مترجم دیگر یک جدول خالی نمی‌بیند و لازم نیست بداند کدام دکمه را باید بزند.\n"
    "        LoadRefEntries();\n        UpdateState();\n    }")

# ---------- ۵) برچسب راهنما → فیلد _lblHint ----------
i, j = cut(data, "        var hint = new Label", "        gridTbl.Controls.Add(hint, 0, 2);")
new_hint = '''        _lblHint = new Label
        {
            Text = "راهنما: جدول از همان باز شدن برنامه با همهٔ کلیدهای مرجع سامانه پیش می‌آید («بستهٔ جدید» هم همین کار را می‌کند)؛ «پرکردن از مرجع» ردیف‌های خالی را با متن مرجع انگلیسی پر می‌کند تا ویرایشش سریع‌تر شود. کلیدِ ترجمه‌نشده در سامانه به فارسی (مبدأ) نمایش داده می‌شود — پوشش تدریجی امن است.",
            AutoSize = true, Padding = new Padding(0, 6, 0, 0), ForeColor = Color.DimGray,
        };
        gridTbl.Controls.Add(_lblHint, 0, 2);'''
data = data[:i] + new_hint + data[j:]

# ---------- ۶) NewPack → LoadRefEntries مشترک ----------
i, j = cut(data, "    private void NewPack(bool fromKeys)", "    /// <summary>\n    /// فاز ۷۸b — پرکردن ردیف‌های خالی از مرجع انگلیسی")
new_newpack = '''    /// <summary>فاز ۷۹ — پیش‌آوردن همهٔ کلیدهای مرجع (در باز شدن برنامه و در «بستهٔ جدید»).
    /// کد/نام/جهت خالی می‌ماند تا مترجم مشخصات زبان تازه را بنویسد.</summary>
    private void LoadRefEntries()
    {
        _entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var k in Ref.Keys) _entries[k] = "";
        _code = ""; _title = ""; _dir = "ltr"; _path = null;
        _cmbCode.Text = ""; _cmbTitle.Text = ""; _cmbDir.SelectedItem = "ltr";
        _dirty = false;
        if (Ref.Count == 0)
        {
            // فاز ۷۹ — دیگر «خالی خاموش» نیست: هشدار صریح با راهنمای رفع
            _lblHint.Text = "⚠ فهرست مرجع سامانه (ref-en.json) بارگذاری نشد — جدول خالی می‌ماند. فایل ref-en.json را کنار TranslationStudio.exe بگذارید و برنامه را دوباره باز کنید.";
            _lblHint.ForeColor = Color.Firebrick;
        }
        RefillGrid();
        UpdateState();
    }

    private void NewPack(bool fromKeys)
    {
        if (fromKeys) { OpenForKeys(); return; }
        if (_dirty && !ConfirmDiscard()) return;
        // فاز ۷۸b — «بستهٔ جدید» دیگر خالی نیست: همهٔ کلیدهای مرجع سامانه (متن‌های مبدأ فارسی)
        // با ستون مرجع (English) از منبع جاسازی‌شده پیش می‌آیند تا مترجم بلافاصله ترجمه کند.
        LoadRefEntries();
        if (Ref.Count > 0)
            MessageBox.Show(this,
                $"{ToPersianDigits(Ref.Count.ToString())} کلید مرجع سامانه بارگذاری شد — ستون مرجع (English) برای راهنمایی مترجم پیشِ روست.",
                "بستهٔ جدید", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

'''
data = data[:i] + new_newpack + data[j:]

io.open(P, "w", encoding="utf-8", newline="\n").write(data)
print("MainForm.cs patched OK —", len(data), "bytes")

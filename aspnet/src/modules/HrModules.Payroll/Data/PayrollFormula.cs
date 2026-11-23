using System.Globalization;
using Platform.Web.Services;

namespace HrModules.Payroll.Data;

/// <summary>خطای فرمول — پیام فارسی برای نمایش در پنل فرمول‌ساز.</summary>
public class PayFormulaException : Exception
{
    public PayFormulaException(string message) : base(message) { }
}

public enum PayTokenKind { Num, Ident, Str, Op, LParen, RParen, Comma, End }

public sealed record PayToken(PayTokenKind Kind, string Text, decimal Num);

/// <summary>مبنای ارزیابی فرمول: متغیرهای زمینه + مقادیر اقلامِ محاسبه‌شدهٔ قبلی + پلکان مالیات.</summary>
public sealed class PayFormulaContext
{
    /// <summary>متغیرهای عددی (پایه، کارکرد، پارامترهای سالانه، جمع‌های لحظه‌ای…)</summary>
    public required IReadOnlyDictionary<string, decimal> Vars { get; init; }
    /// <summary>مقادیر اقلام محاسبه‌شدهٔ قبلی برای تابع قلم("کد")</summary>
    public IReadOnlyDictionary<string, decimal>? Lines { get; init; }
    /// <summary>پلکان مالیات (سقف ریالی، نرخ درصدی) — سقف ۰ یعنی نامحدود</summary>
    public IReadOnlyList<(decimal UpTo, decimal Rate)>? TaxBrackets { get; init; }
}

/// <summary>
/// ارزیاب فرمول امن بدون وابستگی خارجی — موتور فرمول‌ساز حقوق (فاز ۲۴).
/// شناسه‌های فارسی، عملگرهای حسابی/مقایسه‌ای/منطقی، توان، و توابع:
/// round / min / max / abs / ceil / floor / if / قلم("کد") / مالیات(مبلغ).
/// هیچ دسترسی به فایل، شبکه یا انعکاس ندارد — فقط محاسبهٔ عددی.
/// </summary>
public static class PayrollFormula
{
    // ---------- نرمال‌سازی ورودی فارسی ----------

    /// <summary>نیم‌فاصله→زیرخط، ارقام فارسی→لاتین، ٫→نقطه، ي/ك عربی→فارسی.</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var ch in input)
        {
            sb.Append(ch switch
            {
                '\u200C' or '\u200F' or '\u200E' => '_', // نیم‌فاصله → زیرخطِ شناسه
                '\u066B' => '.',
                'ك' => 'ک',
                'ي' => 'ی',
                '۰' or '٠' => '0',
                '۱' or '١' => '1',
                '۲' or '٢' => '2',
                '۳' or '٣' => '3',
                '۴' or '٤' => '4',
                '۵' or '٥' => '5',
                '۶' or '٦' => '6',
                '۷' or '٧' => '7',
                '۸' or '٨' => '8',
                '۹' or '٩' => '9',
                _ => ch,
            });
        }
        return sb.ToString();
    }

    // ---------- توکنایزر ----------

    private static readonly string[] TwoCharOps = { "<=", ">=", "==", "!=", "&&", "||" };
    private static readonly string SingleChars = "+-*/%^<>(),!";

    private static List<PayToken> Tokenize(string src)
    {
        var tokens = new List<PayToken>();
        int i = 0;
        while (i < src.Length)
        {
            var c = src[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < src.Length && char.IsAsciiDigit(src[i + 1])))
            {
                var start = i;
                while (i < src.Length && (char.IsAsciiDigit(src[i]) || src[i] == '.')) i++;
                var text = src[start..i];
                if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var num))
                    throw new PayFormulaException(HrT.F("عدد «{0}» نامعتبر است", text));
                tokens.Add(new PayToken(PayTokenKind.Num, text, num));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < src.Length && (char.IsLetterOrDigit(src[i]) || src[i] == '_')) i++;
                tokens.Add(new PayToken(PayTokenKind.Ident, src[start..i], 0));
                continue;
            }

            if (c == '\'')
            {
                var start = ++i;
                while (i < src.Length && src[i] != '\'') i++;
                if (i >= src.Length) throw new PayFormulaException(HrT.T("رشته بسته نشده است — علامت ' جا افتاده"));
                tokens.Add(new PayToken(PayTokenKind.Str, src[start..i], 0));
                i++;
                continue;
            }

            if (i + 1 < src.Length)
            {
                var two = src.Substring(i, 2);
                if (TwoCharOps.Contains(two))
                {
                    tokens.Add(new PayToken(PayTokenKind.Op, two, 0));
                    i += 2;
                    continue;
                }
            }

            if (SingleChars.Contains(c))
            {
                tokens.Add(new PayToken(
                    c == '(' ? PayTokenKind.LParen : c == ')' ? PayTokenKind.RParen : c == ',' ? PayTokenKind.Comma : PayTokenKind.Op,
                    c.ToString(), 0));
                i++;
                continue;
            }

            throw new PayFormulaException(HrT.F("کاراکتر غیرمجاز «{0}» در فرمول", c));
        }
        tokens.Add(new PayToken(PayTokenKind.End, "", 0));
        return tokens;
    }

    // ---------- AST ----------

    private abstract class Node { }
    private sealed class NumNode : Node { public decimal V; }
    private sealed class StrNode : Node { public string S = ""; }
    private sealed class VarNode : Node { public string Name = ""; }
    private sealed class UnaryNode : Node { public string Op = ""; public Node X = null!; }
    private sealed class BinaryNode : Node { public string Op = ""; public Node L = null!; public Node R = null!; }
    private sealed class CallNode : Node { public string Name = ""; public List<Node> Args = new(); }

    // ---------- پارسر (تقدم عملگرها) ----------

    private sealed class Parser
    {
        private readonly List<PayToken> _t;
        private int _p;
        private Parser(List<PayToken> t) { _t = t; }

        public static Node Parse(string formula)
        {
            var p = new Parser(Tokenize(formula));
            var node = p.Expr();
            if (p.Cur().Kind != PayTokenKind.End)
                throw new PayFormulaException(HrT.F("عبارت اضافه پس از پایان فرمول: «{0}»", p.Cur().Text));
            return node;
        }

        private PayToken Cur() => _t[_p];
        private PayToken Next() => _t[_p++];

        private bool EatOp(string op)
        {
            var c = Cur();
            if (c.Kind == PayTokenKind.Op && c.Text == op) { _p++; return true; }
            return false;
        }

        private PayToken ExpectOp(string op)
        {
            var c = Cur();
            if (c.Kind == PayTokenKind.Op && c.Text == op) { _p++; return c; }
            throw new PayFormulaException(HrT.F("به «{0}» نیاز است اما «{1}» دیده شد", op, c.Text == "" ? HrT.T("پایان فرمول") : c.Text));
        }

        private Node Expr() => Or();

        private Node Or()
        {
            var l = And();
            while (EatOp("||")) l = new BinaryNode { Op = "||", L = l, R = And() };
            return l;
        }

        private Node And()
        {
            var l = Cmp();
            while (EatOp("&&")) l = new BinaryNode { Op = "&&", L = l, R = Cmp() };
            return l;
        }

        private Node Cmp()
        {
            var l = Add();
            var c = Cur();
            if (c.Kind == PayTokenKind.Op && c.Text is "==" or "!=" or "<" or "<=" or ">" or ">=")
            {
                _p++;
                return new BinaryNode { Op = c.Text, L = l, R = Add() };
            }
            return l;
        }

        private Node Add()
        {
            var l = Mul();
            while (true)
            {
                var c = Cur();
                if (c.Kind == PayTokenKind.Op && (c.Text == "+" || c.Text == "-"))
                {
                    _p++;
                    l = new BinaryNode { Op = c.Text, L = l, R = Mul() };
                }
                else return l;
            }
        }

        private Node Mul()
        {
            var l = Unary();
            while (true)
            {
                var c = Cur();
                if (c.Kind == PayTokenKind.Op && (c.Text == "*" || c.Text == "/" || c.Text == "%"))
                {
                    _p++;
                    l = new BinaryNode { Op = c.Text, L = l, R = Unary() };
                }
                else return l;
            }
        }

        private Node Unary()
        {
            var c = Cur();
            if (c.Kind == PayTokenKind.Op && (c.Text == "-" || c.Text == "!"))
            {
                _p++;
                return new UnaryNode { Op = c.Text, X = Unary() };
            }
            return Power();
        }

        private Node Power()
        {
            var l = Primary();
            if (EatOp("^")) return new BinaryNode { Op = "^", L = l, R = Unary() }; // راست‌گرا
            return l;
        }

        private Node Primary()
        {
            var c = Next();
            switch (c.Kind)
            {
                case PayTokenKind.Num:
                    return new NumNode { V = c.Num };
                case PayTokenKind.Str:
                    return new StrNode { S = c.Text };
                case PayTokenKind.LParen:
                {
                    var e = Expr();
                    if (Cur().Kind != PayTokenKind.RParen)
                        throw new PayFormulaException(HrT.T("پرانتز بسته نشده است"));
                    _p++;
                    return e;
                }
                case PayTokenKind.Ident:
                    if (Cur().Kind == PayTokenKind.LParen)
                    {
                        _p++;
                        var args = new List<Node>();
                        if (Cur().Kind != PayTokenKind.RParen)
                        {
                            args.Add(Expr());
                            while (Cur().Kind == PayTokenKind.Comma) { _p++; args.Add(Expr()); }
                        }
                        if (Cur().Kind != PayTokenKind.RParen)
                            throw new PayFormulaException(HrT.F("پرانتز تابع «{0}» بسته نشده است", c.Text));
                        _p++;
                        return new CallNode { Name = c.Text, Args = args };
                    }
                    return new VarNode { Name = c.Text };
                default:
                    throw new PayFormulaException(c.Text == ""
                        ? HrT.T("فرمول ناقص است — انتهای فرمول نامنتظره")
                        : HrT.F("عبارت نامعتبر: «{0}»", c.Text));
            }
        }
    }

    /// <summary>بررسی نحوی بدون ارزیابی — برای اعتبارسنجی فرم در پنل.</summary>
    public static string? SyntaxCheck(string? formula)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(formula)) return "فرمول خالی است";
            Parser.Parse(Normalize(formula));
            return null;
        }
        catch (PayFormulaException ex) { return ex.Message; }
    }

    // ---------- ارزیاب ----------

    private readonly record struct Val(decimal D, string? S)
    {
        public bool IsStr => S is not null;
        public decimal Num => IsStr ? throw new PayFormulaException(HrT.T("در اینجا فقط عدد مجاز است، رشته داده شد")) : D;
        public static Val Of(decimal d) => new(d, null);
        public static Val Of(string s) => new(0m, s);
    }

    public static decimal Evaluate(string? formula, PayFormulaContext ctx)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new PayFormulaException(HrT.T("فرمول خالی است"));
        var v = Eval(Parser.Parse(Normalize(formula)), ctx);
        if (v.IsStr) throw new PayFormulaException(HrT.T("نتیجه فرمول رشته است — فرمول باید عدد برگرداند"));
        return v.D;
    }

    private static Val Eval(Node node, PayFormulaContext ctx) => node switch
    {
        NumNode n => Val.Of(n.V),
        StrNode s => Val.Of(s.S),
        VarNode v => Lookup(v.Name, ctx),
        UnaryNode u => EvalUnary(u, ctx),
        BinaryNode b => EvalBinary(b, ctx),
        CallNode c => EvalCall(c, ctx),
        _ => throw new PayFormulaException(HrT.T("گره نامعتبر در درخت فرمول")),
    };

    private static Val Lookup(string name, PayFormulaContext ctx)
    {
        if (ctx.Vars.TryGetValue(name, out var d)) return Val.Of(d);
        throw new PayFormulaException(HrT.F("متغیر «{0}» تعریف نشده است", name));
    }

    private static Val EvalUnary(UnaryNode u, PayFormulaContext ctx)
    {
        var x = Eval(u.X, ctx);
        if (u.Op == "-") return Val.Of(-x.Num);
        return Val.Of(x.IsStr ? (x.S!.Length > 0 ? 0m : 1m) : (x.D == 0 ? 1m : 0m)); // !
    }

    private static Val EvalBinary(BinaryNode b, PayFormulaContext ctx)
    {
        if (b.Op == "&&")
        {
            var l = Eval(b.L, ctx);
            return l.D == 0 ? Val.Of(0m) : Val.Of(Eval(b.R, ctx).D == 0 ? 0m : 1m);
        }
        if (b.Op == "||")
        {
            var l = Eval(b.L, ctx);
            return l.D != 0 ? Val.Of(1m) : Val.Of(Eval(b.R, ctx).D != 0 ? 1m : 0m);
        }
        var a = Eval(b.L, ctx);
        var c = Eval(b.R, ctx);

        switch (b.Op)
        {
            case "==": case "!=":
                var eq = a.IsStr || c.IsStr
                    ? string.Equals(a.S ?? a.D.ToString(CultureInfo.InvariantCulture), c.S ?? c.D.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    : a.D == c.D;
                return Val.Of(eq == (b.Op == "==") ? 1m : 0m);
            case "<": case "<=": case ">": case ">=":
            {
                var ld = a.Num; var rd = c.Num;
                return Val.Of(b.Op switch
                {
                    "<" => ld < rd ? 1m : 0m,
                    "<=" => ld <= rd ? 1m : 0m,
                    ">" => ld > rd ? 1m : 0m,
                    _ => ld >= rd ? 1m : 0m,
                });
            }
            case "+": return Val.Of(a.Num + c.Num);
            case "-": return Val.Of(a.Num - c.Num);
            case "*": return Val.Of(a.Num * c.Num);
            case "%":
                if (c.D == 0) throw new PayFormulaException(HrT.T("باقیمانده بر صفر ممکن نیست"));
                return Val.Of(a.Num % c.Num);
            case "/":
                if (c.D == 0) throw new PayFormulaException(HrT.T("تقسیم بر صفر در فرمول"));
                return Val.Of(a.Num / c.Num);
            case "^": return Val.Of(Pow(a.Num, c.Num));
            default:
                throw new PayFormulaException(HrT.F("عملگر «{0}» پشتیبانی نمی‌شود", b.Op));
        }
    }

    private static decimal Pow(decimal b, decimal e)
    {
        if (e == 0m) return 1m;
        if (e < 0m) throw new PayFormulaException(HrT.T("توان منفی پشتیبانی نمی‌شود"));
        var exp = (int)e;
        if (exp != e) throw new PayFormulaException(HrT.T("توان باید عدد صحیح باشد"));
        decimal r = 1m;
        for (int i = 0; i < exp; i++) r *= b;
        return r;
    }

    private static Val EvalCall(CallNode c, PayFormulaContext ctx) => c.Name switch
    {
        "round" => Val.Of(Round(NumArg(c, ctx, 0), c.Args.Count > 1 ? (int)NumArg(c, ctx, 1) : 0)),
        "min" => Val.Of(c.Args.Select(a => Eval(a, ctx).Num).Aggregate((x, y) => Math.Min(x, y))),
        "max" => Val.Of(c.Args.Select(a => Eval(a, ctx).Num).Aggregate((x, y) => Math.Max(x, y))),
        "abs" => Val.Of(Math.Abs(NumArg(c, ctx, 0))),
        "ceil" => Val.Of(Math.Ceiling(NumArg(c, ctx, 0))),
        "floor" => Val.Of(Math.Floor(NumArg(c, ctx, 0))),
        "if" => Eval(c.Args[0], ctx).D != 0 ? Eval(c.Args[1], ctx) : Eval(c.Args[2], ctx),
        "قلم" => Val.Of(LineOf(StrArg(c, ctx, "قلم"), ctx)),
        "مالیات" => Val.Of(ProgressiveTax(NumArg(c, ctx, 0), ctx)),
        _ => throw new PayFormulaException(HrT.F("تابع «{0}» شناخته‌شده نیست", c.Name)),
    };

    private static decimal NumArg(CallNode c, PayFormulaContext ctx, int i)
    {
        if (i >= c.Args.Count)
            throw new PayFormulaException(HrT.F("آرگومان {0} تابع «{1}» کم است", i + 1, c.Name));
        return Eval(c.Args[i], ctx).Num;
    }

    private static string StrArg(CallNode c, PayFormulaContext ctx, string fn)
    {
        if (c.Args.Count < 1) throw new PayFormulaException(HrT.F("تابع «{0}» به آرگومان رشته‌ای نیاز دارد", fn));
        var v = Eval(c.Args[0], ctx);
        if (!v.IsStr) throw new PayFormulaException(HrT.F("آرگومان تابع «{0}» باید رشته باشد — مثال: {1}('کد')", fn, fn));
        return v.S!;
    }

    private static decimal Round(decimal x, int digits) =>
        Math.Round(x, Math.Clamp(digits, 0, 6), MidpointRounding.AwayFromZero);

    private static decimal LineOf(string code, PayFormulaContext ctx) =>
        ctx.Lines is not null && ctx.Lines.TryGetValue(code, out var v) ? v : 0m;

    /// <summary>مالیات پلکانی — مبلغ مشمول (پس از معافیت) × نرخ‌های هر پله؛ نرخ‌ها درصد.</summary>
    private static decimal ProgressiveTax(decimal taxable, PayFormulaContext ctx)
    {
        var brackets = ctx.TaxBrackets;
        if (brackets is null || brackets.Count == 0)
            throw new PayFormulaException(HrT.T("پارامتر «پلکان_مالیات» برای این شرکت/سال تعریف نشده است"));
        if (taxable <= 0) return 0;
        decimal prev = 0, tax = 0;
        foreach (var (upTo, rate) in brackets)
        {
            var upper = upTo <= 0 ? decimal.MaxValue : upTo;
            if (taxable > prev)
            {
                var slice = Math.Min(taxable, upper) - prev;
                tax += slice * rate / 100m;
            }
            prev = upper;
            if (taxable <= upper) break;
        }
        return tax;
    }
}

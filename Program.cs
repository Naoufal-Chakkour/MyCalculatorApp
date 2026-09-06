// Program.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

app.UseDefaultFiles();
app.UseStaticFiles();

// ============================================================
// REQUEST HELPERS
// ============================================================
static async Task<Dictionary<string, string>> ReadRequestData(HttpContext context)
{
    var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    if (context.Request.HasJsonContentType())
    {
        using JsonDocument document = await JsonDocument.ParseAsync(context.Request.Body);
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                data[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? ""
                    : property.Value.ToString();
            }
        }
        return data;
    }

    if (context.Request.HasFormContentType)
    {
        var form = await context.Request.ReadFormAsync();
        foreach (var item in form) data[item.Key] = item.Value.ToString();
        return data;
    }

    foreach (var item in context.Request.Query) data[item.Key] = item.Value.ToString();
    return data;
}

static string GetValue(Dictionary<string, string> data, params string[] keys)
{
    foreach (string key in keys)
        if (data.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            return value;
    return "";
}

// ============================================================
// CALCULATOR
// ============================================================
app.MapPost("/calculate", async (HttpContext context) =>
{
    context.Response.ContentType = "text/plain; charset=utf-8";
    try
    {
        var data = await ReadRequestData(context);
        string expr = GetValue(data, "expr", "expression");
        if (string.IsNullOrWhiteSpace(expr)) throw new Exception("Empty expression");
        if (expr.Length > 5000) throw new Exception("Expression too long");

        double result = ExpressionEvaluator.Evaluate(expr);
        if (!double.IsFinite(result)) throw new Exception("Invalid result");

        await context.Response.WriteAsync(Format.Number(result));
    }
    catch
    {
        await context.Response.WriteAsync("خطأ");
    }
});

// ============================================================
// QUADRATIC
// ============================================================
app.MapPost("/solve-quadratic", async (HttpContext context) =>
{
    context.Response.ContentType = "application/json; charset=utf-8";
    try
    {
        var data = await ReadRequestData(context);
        string aText = GetValue(data, "a", "A");
        string bText = GetValue(data, "b", "B");
        string cText = GetValue(data, "c", "C");

        if (string.IsNullOrWhiteSpace(aText) || string.IsNullOrWhiteSpace(bText) || string.IsNullOrWhiteSpace(cText))
            throw new Exception("Missing coefficients");

        double a = ExpressionEvaluator.Evaluate(aText);
        double b = ExpressionEvaluator.Evaluate(bText);
        double c = ExpressionEvaluator.Evaluate(cText);

        if (!double.IsFinite(a) || !double.IsFinite(b) || !double.IsFinite(c))
            throw new Exception("Invalid coefficients");

        QuadraticResult result = EquationSolver.SolveQuadratic(a, b, c);
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, jsonOptions));
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync(JsonSerializer.Serialize(new QuadraticResult
        {
            Radical = "خطأ",
            DecimalVal = "خطأ",
            Steps = "حدث خطأ أثناء حل المعادلة.\n\nتأكد من صحة a و b و c.\n\nالتفاصيل: " + ex.Message
        }, jsonOptions));
    }
});

// ============================================================
// UNIVERSAL EQUATION
// ============================================================
app.MapPost("/solve-equation", async (HttpContext context) =>
{
    context.Response.ContentType = "application/json; charset=utf-8";
    try
    {
        var data = await ReadRequestData(context);
        string expr = GetValue(data, "expr", "expression", "equation");
        if (string.IsNullOrWhiteSpace(expr)) throw new Exception("Empty equation");
        if (expr.Length > 10000) throw new Exception("Equation too long");

        EquationResult result = UniversalEquationSolver.Solve(expr);
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, jsonOptions));
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync(JsonSerializer.Serialize(new EquationResult
        {
            Result = "تعذر حل المعادلة",
            Steps = "حدث خطأ أثناء تحليل المعادلة.\n\nأمثلة:\n2x+5=17\nx^2-5x+6=0\n" +
                    "x^3-6x^2+11x-6=0\nsin(x)=0.5\ncos(x)=0\ntan(x)=1\nln(x)=2\nlog(x)=2\n" +
                    "sqrt(x+1)=3\n2^x=16\n\nالتفاصيل: " + ex.Message
        }, jsonOptions));
    }
});

// ============================================================
// BACKWARD COMPATIBILITY
// ============================================================
app.MapPost("/solve-polynomial-expr", async (HttpContext context) =>
{
    context.Response.ContentType = "application/json; charset=utf-8";
    try
    {
        var data = await ReadRequestData(context);
        string expr = GetValue(data, "expr", "expression", "equation");
        if (string.IsNullOrWhiteSpace(expr)) throw new Exception("Empty equation");

        EquationResult result = UniversalEquationSolver.Solve(expr);
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, jsonOptions));
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync(JsonSerializer.Serialize(new EquationResult
        {
            Result = "تعذر حل المعادلة",
            Steps = "تأكد من صحة المعادلة.\n\nالتفاصيل: " + ex.Message
        }, jsonOptions));
    }
});

app.Run();

// ============================================================
// RESULT MODELS
// ============================================================
class QuadraticResult
{
    public string Radical { get; set; } = "";
    public string DecimalVal { get; set; } = "";
    public string Steps { get; set; } = "";
}

class EquationResult
{
    public string Result { get; set; } = "";
    public string Steps { get; set; } = "";
}

// ============================================================
// FORMAT
// ============================================================
static class Format
{
    public static string Number(double number)
    {
        if (double.IsNaN(number)) return "غير معرف";
        if (double.IsPositiveInfinity(number)) return "∞";
        if (double.IsNegativeInfinity(number)) return "-∞";
        if (Math.Abs(number) < 1e-10) number = 0;

        if (Math.Abs(number - Math.Round(number)) < 1e-9)
            return Math.Round(number).ToString(CultureInfo.InvariantCulture);

        return number.ToString("0.##########", CultureInfo.InvariantCulture);
    }

    public static string Fraction(double number)
    {
        if (!double.IsFinite(number)) return Number(number);
        if (Math.Abs(number - Math.Round(number)) < 1e-9)
            return Math.Round(number).ToString(CultureInfo.InvariantCulture);

        var fraction = FractionHelper.ToFraction(number);
        if (fraction.den <= 0) return Number(number);
        if (fraction.den == 1) return fraction.num.ToString();
        return $"{fraction.num}/{fraction.den}";
    }

    public static string Signed(double number)
    {
        if (number >= 0) return "+ " + Fraction(number);
        return "- " + Fraction(Math.Abs(number));
    }

    public static string Complex(Complex z)
    {
        string real = Fraction(z.Real);
        if (Math.Abs(z.Imaginary) < 1e-8) return real;

        string imaginary = Fraction(Math.Abs(z.Imaginary));
        if (Math.Abs(z.Real) < 1e-8)
            return z.Imaginary >= 0 ? $"{imaginary}i" : $"-{imaginary}i";

        return z.Imaginary >= 0 ? $"{real} + {imaginary}i" : $"{real} - {imaginary}i";
    }
}

// ============================================================
// FRACTION
// ============================================================
static class FractionHelper
{
    public static (long num, long den) ToFraction(double number, int maxDenominator = 1000)
    {
        if (!double.IsFinite(number)) return (0, 0);

        long sign = number < 0 ? -1 : 1;
        double absolute = Math.Abs(number);

        long bestNum = 0, bestDen = 1;
        double bestError = double.MaxValue;

        for (long den = 1; den <= maxDenominator; den++)
        {
            long num = (long)Math.Round(absolute * den);
            double error = Math.Abs(absolute - (double)num / den);
            if (error < bestError) { bestError = error; bestNum = num; bestDen = den; }
            if (error < 1e-10) break;
        }

        if (bestError > 1e-7) return (0, 0);

        long gcd = Gcd(bestNum, bestDen);
        return (sign * bestNum / gcd, bestDen / gcd);
    }

    static long Gcd(long a, long b)
    {
        a = Math.Abs(a); b = Math.Abs(b);
        while (b != 0) (a, b) = (b, a % b);
        return a == 0 ? 1 : a;
    }
}

// ============================================================
// EXPRESSION AST
// ============================================================
abstract class ExprNode
{
    public abstract double Evaluate(double x);
    public virtual bool ContainsX => false;
}

class ConstantNode : ExprNode
{
    public double Value { get; }
    public ConstantNode(double value) { Value = value; }
    public override double Evaluate(double x) => Value;
}

class VariableNode : ExprNode
{
    public override double Evaluate(double x) => x;
    public override bool ContainsX => true;
}

class UnaryNode : ExprNode
{
    public char Operator { get; }
    public ExprNode Operand { get; }
    public UnaryNode(char op, ExprNode operand) { Operator = op; Operand = operand; }
    public override double Evaluate(double x)
    {
        double value = Operand.Evaluate(x);
        return Operator == '-' ? -value : value;
    }
    public override bool ContainsX => Operand.ContainsX;
}

class BinaryNode : ExprNode
{
    public char Operator { get; }
    public ExprNode Left { get; }
    public ExprNode Right { get; }
    public BinaryNode(char op, ExprNode left, ExprNode right) { Operator = op; Left = left; Right = right; }

    public override double Evaluate(double x)
    {
        double left = Left.Evaluate(x);
        double right = Right.Evaluate(x);
        try
        {
            return Operator switch
            {
                '+' => left + right,
                '-' => left - right,
                '*' => left * right,
                '/' => Math.Abs(right) < 1e-15 ? double.NaN : left / right,
                '%' => Math.Abs(right) < 1e-15 ? double.NaN : left % right,
                '^' => Math.Pow(left, right),
                _ => double.NaN
            };
        }
        catch { return double.NaN; }
    }

    public override bool ContainsX => Left.ContainsX || Right.ContainsX;
}

// ===== FunctionNode (تم تصحيحها: الدوال المثلثية تعمل بالدرجات دائمًا، بشكل متسق مع
//        محلل المعادلات الخاصة SpecialEquationSolver الذي يفترض ذلك) =====
class FunctionNode : ExprNode
{
    public string Name { get; }
    public ExprNode Argument { get; }
    public FunctionNode(string name, ExprNode argument) { Name = name; Argument = argument; }

    public override double Evaluate(double x)
    {
        double value = Argument.Evaluate(x);
        const double deg2rad = Math.PI / 180.0;

        try
        {
            return Name.ToLowerInvariant() switch
            {
                // sin/cos/tan تأخذ درجات
                "sin" => Math.Sin(value * deg2rad),
                "cos" => Math.Cos(value * deg2rad),
                "tan" => Math.Tan(value * deg2rad),

                // الدوال العكسية ترجع درجات
                "asin" when value >= -1 && value <= 1 => Math.Asin(value) / deg2rad,
                "acos" when value >= -1 && value <= 1 => Math.Acos(value) / deg2rad,
                "atan" => Math.Atan(value) / deg2rad,

                "log" when value > 0 => Math.Log10(value),
                "ln" when value > 0 => Math.Log(value),
                "sqrt" when value >= 0 => Math.Sqrt(value),
                "exp" => Math.Exp(value),
                "abs" => Math.Abs(value),
                _ => double.NaN
            };
        }
        catch { return double.NaN; }
    }

    public override bool ContainsX => Argument.ContainsX;
}

// ============================================================
// EXPRESSION PARSER
// ============================================================
static class ExpressionParser
{
    static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    { "sin", "cos", "tan", "asin", "acos", "atan", "log", "ln", "sqrt", "exp", "abs" };

    public static ExprNode Parse(string input)
    {
        string text = Normalize(input);
        if (string.IsNullOrWhiteSpace(text)) throw new Exception("Empty expression");

        Parser parser = new Parser(text);
        ExprNode result = parser.ParseExpression();

        if (!parser.End) throw new Exception($"Unexpected characters at position {parser.Position}");
        return result;
    }

    static string Normalize(string text)
    {
        return text.Replace(" ", "").Replace("×", "*").Replace("÷", "/").Replace("−", "-")
            .Replace("π", "pi").Replace("Π", "pi")
            .Replace("²", "^2").Replace("³", "^3").Replace("⁴", "^4").Replace("⁵", "^5")
            .Replace(",", ".");
    }

    class Parser
    {
        readonly string text;
        int position;
        public int Position => position;
        public bool End => position >= text.Length;

        public Parser(string value) { text = value; position = 0; }

        char? Peek() => position >= text.Length ? null : text[position];

        bool Match(char c)
        {
            if (Peek() == c) { position++; return true; }
            return false;
        }

        public ExprNode ParseExpression()
        {
            ExprNode node = ParseTerm();
            while (Peek() == '+' || Peek() == '-')
            {
                char op = text[position++];
                ExprNode right = ParseTerm();
                node = new BinaryNode(op, node, right);
            }
            return node;
        }

        ExprNode ParseTerm()
        {
            ExprNode node = ParseUnary();
            while (true)
            {
                char? c = Peek();
                if (c == '*' || c == '/' || c == '%')
                {
                    position++;
                    ExprNode right = ParseUnary();
                    node = new BinaryNode(c.Value, node, right);
                    continue;
                }
                if (StartsImplicitFactor())
                {
                    ExprNode right = ParseUnary();
                    node = new BinaryNode('*', node, right);
                    continue;
                }
                break;
            }
            return node;
        }

        bool StartsImplicitFactor()
        {
            char? c = Peek();
            if (!c.HasValue) return false;
            return char.IsDigit(c.Value) || c == '.' || c == '(' || char.IsLetter(c.Value);
        }

        ExprNode ParseUnary()
        {
            if (Match('-')) return new UnaryNode('-', ParseUnary());
            if (Match('+')) return new UnaryNode('+', ParseUnary());
            return ParsePower();
        }

        ExprNode ParsePower()
        {
            ExprNode node = ParseFactor();
            if (Match('^'))
            {
                ExprNode exponent = ParseUnary();
                node = new BinaryNode('^', node, exponent);
            }
            return node;
        }

        ExprNode ParseFactor()
        {
            if (Match('('))
            {
                ExprNode node = ParseExpression();
                if (!Match(')')) throw new Exception("Missing ')'");
                return node;
            }

            char? current = Peek();
            if (current.HasValue && char.IsLetter(current.Value)) return ParseIdentifier();
            return ParseNumber();
        }

        ExprNode ParseIdentifier()
        {
            int start = position;
            while (position < text.Length && char.IsLetter(text[position])) position++;
            string name = text[start..position];

            if (name.Equals("x", StringComparison.OrdinalIgnoreCase)) return new VariableNode();
            if (name.Equals("pi", StringComparison.OrdinalIgnoreCase)) return new ConstantNode(Math.PI);
            if (name.Equals("e", StringComparison.OrdinalIgnoreCase)) return new ConstantNode(Math.E);

            if (!Functions.Contains(name)) throw new Exception($"Unknown identifier: {name}");
            if (!Match('(')) throw new Exception($"Function {name} requires parentheses");

            ExprNode argument = ParseExpression();
            if (!Match(')')) throw new Exception("Missing ')'");
            return new FunctionNode(name, argument);
        }

        ExprNode ParseNumber()
        {
            int start = position;
            bool hasDigit = false;

            while (position < text.Length && char.IsDigit(text[position])) { position++; hasDigit = true; }

            if (position < text.Length && text[position] == '.')
            {
                position++;
                while (position < text.Length && char.IsDigit(text[position])) { position++; hasDigit = true; }
            }

            if (!hasDigit) throw new Exception($"Invalid number at position {position}");

            if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
            {
                int exponentPosition = position;
                position++;
                if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++;
                int exponentStart = position;
                while (position < text.Length && char.IsDigit(text[position])) position++;
                if (exponentStart == position) position = exponentPosition;
            }

            string number = text[start..position];
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new Exception($"Invalid number: {number}");

            return new ConstantNode(value);
        }
    }
}

// ============================================================
// EXPRESSION EVALUATOR
// ============================================================
static class ExpressionEvaluator
{
    public static double Evaluate(string expression)
    {
        ExprNode tree = ExpressionParser.Parse(expression);
        double result = tree.Evaluate(0);
        if (!double.IsFinite(result)) throw new Exception("Invalid result");
        return result;
    }
}

// ============================================================
// POLYNOMIAL
// ============================================================
class Poly
{
    public double[] C { get; }
    public Poly(params double[] coefficients) { C = Trim(coefficients); }

    static double[] Trim(double[] coefficients)
    {
        if (coefficients.Length == 0) return new[] { 0.0 };
        int last = coefficients.Length - 1;
        while (last > 0 && Math.Abs(coefficients[last]) < 1e-12) last--;
        return coefficients.Take(last + 1).ToArray();
    }

    public static Poly Constant(double value) => new Poly(value);
    public static Poly X => new Poly(0, 1);

    public static Poly operator +(Poly a, Poly b)
    {
        int count = Math.Max(a.C.Length, b.C.Length);
        double[] result = new double[count];
        for (int i = 0; i < count; i++)
            result[i] = (i < a.C.Length ? a.C[i] : 0) + (i < b.C.Length ? b.C[i] : 0);
        return new Poly(result);
    }

    public static Poly operator -(Poly a, Poly b) => a + (-b);
    public static Poly operator -(Poly a) => a * -1;

    public static Poly operator *(Poly a, Poly b)
    {
        double[] result = new double[a.C.Length + b.C.Length - 1];
        for (int i = 0; i < a.C.Length; i++)
            for (int j = 0; j < b.C.Length; j++)
                result[i + j] += a.C[i] * b.C[j];
        return new Poly(result);
    }

    public static Poly operator *(Poly a, double value) => new Poly(a.C.Select(x => x * value).ToArray());

    public static Poly operator /(Poly a, double value)
    {
        if (Math.Abs(value) < 1e-15) throw new DivideByZeroException();
        return new Poly(a.C.Select(x => x / value).ToArray());
    }

    public Poly Pow(int exponent)
    {
        if (exponent < 0 || exponent > 20) throw new Exception("Power out of range");
        Poly result = Constant(1);
        for (int i = 0; i < exponent; i++) result *= this;
        return result;
    }

    public int Degree => C.Length - 1;
}

// ============================================================
// POLYNOMIAL CONVERTER
// ============================================================
static class PolynomialConverter
{
    public static bool TryConvert(ExprNode node, out Poly polynomial)
    {
        try
        {
            if (node is ConstantNode constant) { polynomial = Poly.Constant(constant.Value); return true; }
            if (node is VariableNode) { polynomial = Poly.X; return true; }

            if (node is UnaryNode unary)
            {
                if (!TryConvert(unary.Operand, out Poly operand)) { polynomial = Poly.Constant(0); return false; }
                polynomial = unary.Operator == '-' ? -operand : operand;
                return true;
            }

            if (node is BinaryNode binary)
            {
                bool leftOK = TryConvert(binary.Left, out Poly left);
                bool rightOK = TryConvert(binary.Right, out Poly right);
                if (!leftOK || !rightOK) { polynomial = Poly.Constant(0); return false; }

                switch (binary.Operator)
                {
                    case '+': polynomial = left + right; return true;
                    case '-': polynomial = left - right; return true;
                    case '*': polynomial = left * right; return true;

                    case '/':
                        if (right.Degree != 0 || Math.Abs(right.C[0]) < 1e-15) { polynomial = Poly.Constant(0); return false; }
                        polynomial = left / right.C[0];
                        return true;

                    case '^':
                        if (right.Degree != 0) { polynomial = Poly.Constant(0); return false; }
                        double exponent = right.C[0];
                        if (!double.IsFinite(exponent) || Math.Abs(exponent - Math.Round(exponent)) > 1e-10)
                        { polynomial = Poly.Constant(0); return false; }
                        int power = (int)Math.Round(exponent);
                        if (power < 0 || power > 20) { polynomial = Poly.Constant(0); return false; }
                        polynomial = left.Pow(power);
                        return true;
                }
            }
        }
        catch { }

        polynomial = Poly.Constant(0);
        return false;
    }
}

// ============================================================
// QUADRATIC SOLVER (تم تصحيحها: صيغة جذرية رمزية حقيقية + خطوات أوضح)
// ============================================================
static class EquationSolver
{
    // يبسط الجذر: √n = outside × √inside (مثلاً √20 = 2√5). يرجع inside=1 إذا كان الجذر تامًا
    static (long outside, long inside) SimplifySqrt(double value)
    {
        if (value < 0) return (1, 0);
        long n = (long)Math.Round(value);
        if (n == 0) return (0, 0);
        if (Math.Abs(n - value) > 1e-6) return (1, n); // مو صحيحًا تمامًا، لا نحاول التبسيط الرمزي

        long outside = 1, inside = n;
        for (long i = 2; i * i <= inside; i++)
        {
            while (inside % (i * i) == 0) { inside /= (i * i); outside *= i; }
        }
        return (outside, inside);
    }

    static string SqrtSymbol(long outside, long inside)
    {
        if (inside == 1) return outside.ToString(CultureInfo.InvariantCulture);
        return outside > 1 ? $"{outside}√{inside}" : $"√{inside}";
    }

    public static QuadraticResult SolveQuadratic(double a, double b, double c)
    {
        var steps = new StringBuilder();
        string Fr(double v) => Format.Fraction(v);

        steps.AppendLine($"المعادلة: {Fr(a)}x² {Format.Signed(b)}x {Format.Signed(c)} = 0");
        steps.AppendLine();

        // ---------------- الحالة الخطية / التناقض ----------------
        if (Math.Abs(a) < 1e-12)
        {
            if (Math.Abs(b) < 1e-12)
            {
                if (Math.Abs(c) < 1e-12)
                {
                    steps.AppendLine("بما أن a = 0 و b = 0 و c = 0:");
                    steps.AppendLine("0 = 0 وهذا صحيح دائمًا مهما كانت قيمة x.");
                    return new QuadraticResult
                    {
                        Radical = "عدد لا نهائي من الحلول",
                        DecimalVal = "عدد لا نهائي من الحلول",
                        Steps = steps.ToString()
                    };
                }

                steps.AppendLine("بما أن a = 0 و b = 0، تصبح المعادلة:");
                steps.AppendLine($"{Fr(c)} = 0");
                steps.AppendLine("وهذه العبارة غير صحيحة مهما كانت قيمة x، فلا يوجد حل.");
                return new QuadraticResult
                {
                    Radical = "لا يوجد حل",
                    DecimalVal = "لا يوجد حل",
                    Steps = steps.ToString()
                };
            }

            double xLin = -c / b;
            steps.AppendLine("بما أن a = 0، تصبح المعادلة من الدرجة الأولى:");
            steps.AppendLine($"{Fr(b)}x {Format.Signed(c)} = 0");
            steps.AppendLine($"{Fr(b)}x = {Fr(-c)}");
            steps.AppendLine($"x = {Fr(-c)} / {Fr(b)}");
            steps.AppendLine($"x = {Fr(xLin)}");

            string linRes = $"x = {Fr(xLin)}";
            return new QuadraticResult { Radical = linRes, DecimalVal = $"x ≈ {Format.Number(xLin)}", Steps = steps.ToString() };
        }

        // ---------------- حساب المميز ----------------
        double delta = b * b - 4 * a * c;
        if (Math.Abs(delta) < 1e-12) delta = 0;

        steps.AppendLine("الخطوة 1: حساب المميز");
        steps.AppendLine("Δ = b² - 4ac");
        steps.AppendLine($"Δ = ({Fr(b)})² - 4×({Fr(a)})×({Fr(c)})");
        steps.AppendLine($"Δ = {Fr(b * b)} - {Fr(4 * a * c)}");
        steps.AppendLine($"Δ = {Fr(delta)}");
        steps.AppendLine();

        // ---------------- حل مضاعف ----------------
        if (delta == 0)
        {
            double x = -b / (2 * a);
            steps.AppendLine("الخطوة 2: بما أن Δ = 0 → حل حقيقي واحد (مضاعف)");
            steps.AppendLine("الصيغة: x = -b / 2a");
            steps.AppendLine($"x = -({Fr(b)}) / (2×{Fr(a)})");
            steps.AppendLine($"x = {Fr(x)}");

            string res = $"x = {Fr(x)} (حل مضاعف)";
            return new QuadraticResult { Radical = res, DecimalVal = $"x ≈ {Format.Number(x)} (حل مضاعف)", Steps = steps.ToString() };
        }

        // ---------------- حلان حقيقيان ----------------
        if (delta > 0)
        {
            double sqrtD = Math.Sqrt(delta);
            var (outside, inside) = SimplifySqrt(delta);
            bool perfect = inside == 1;

            double x1 = (-b + sqrtD) / (2 * a);
            double x2 = (-b - sqrtD) / (2 * a);

            steps.AppendLine("الخطوة 2: بما أن Δ > 0 → حلان حقيقيان مختلفان");
            steps.AppendLine("الصيغة العامة: x = (-b ± √Δ) / 2a");

            string radical, decimalStr;

            if (perfect)
            {
                steps.AppendLine($"√Δ = √{Fr(delta)} = {sqrtD:F0} (جذر تام)");
                steps.AppendLine($"x1 = (-({Fr(b)}) + {sqrtD:F0}) / (2×{Fr(a)}) = {Fr(x1)}");
                steps.AppendLine($"x2 = (-({Fr(b)}) - {sqrtD:F0}) / (2×{Fr(a)}) = {Fr(x2)}");
                radical = $"x1 = {Fr(x1)}\nx2 = {Fr(x2)}";
                decimalStr = radical;
            }
            else
            {
                string sq = SqrtSymbol(outside, inside);
                double twoA = 2 * a;
                long twoAInt = (long)Math.Round(twoA);
                string denom = Math.Abs(twoA - twoAInt) < 1e-9 ? twoAInt.ToString(CultureInfo.InvariantCulture) : Fr(twoA);
                string bNeg = Fr(-b);

                steps.AppendLine($"Δ ليس جذرًا تامًا، نبسط الجذر: √{Fr(delta)} = {sq}");
                steps.AppendLine($"x1 = ({bNeg} + {sq}) / {denom}");
                steps.AppendLine($"x2 = ({bNeg} - {sq}) / {denom}");
                steps.AppendLine($"القيمة التقريبية: x1 ≈ {Format.Number(x1)} , x2 ≈ {Format.Number(x2)}");

                radical = $"x1 = ({bNeg} + {sq}) / {denom}\nx2 = ({bNeg} - {sq}) / {denom}";
                decimalStr = $"x1 ≈ {Format.Number(x1)}\nx2 ≈ {Format.Number(x2)}";
            }

            return new QuadraticResult { Radical = radical, DecimalVal = decimalStr, Steps = steps.ToString() };
        }

        // ---------------- حلول عقدية ----------------
        {
            double real = -b / (2 * a);
            double imagAbs = Math.Sqrt(-delta) / Math.Abs(2 * a);
            var (outside, inside) = SimplifySqrt(-delta);
            bool perfect = inside == 1;

            steps.AppendLine("الخطوة 2: بما أن Δ < 0 → لا يوجد حلول حقيقية، الحلول عقدية (مركبة)");
            steps.AppendLine("الصيغة: x = (-b ± i√(-Δ)) / 2a");

            string radical, decimalStr;

            if (perfect)
            {
                steps.AppendLine($"√(-Δ) = √{Fr(-delta)} = {Math.Sqrt(-delta):F0} (جذر تام)");
                radical = $"x1 = {Fr(real)} + {Fr(imagAbs)}i\nx2 = {Fr(real)} - {Fr(imagAbs)}i";
                decimalStr = radical;
                steps.AppendLine(radical);
            }
            else
            {
                string sq = SqrtSymbol(outside, inside);
                double twoA = 2 * a;
                long twoAInt = (long)Math.Round(twoA);
                string denom = Math.Abs(twoA - twoAInt) < 1e-9 ? twoAInt.ToString(CultureInfo.InvariantCulture) : Fr(twoA);

                radical = $"x1 = {Fr(real)} + ({sq}/{denom})i\nx2 = {Fr(real)} - ({sq}/{denom})i";
                decimalStr = $"x1 ≈ {Format.Number(real)} + {Format.Number(imagAbs)}i\nx2 ≈ {Format.Number(real)} - {Format.Number(imagAbs)}i";

                steps.AppendLine($"√(-Δ) = √{Fr(-delta)} = {sq}");
                steps.AppendLine(radical);
                steps.AppendLine($"القيمة التقريبية:\n{decimalStr}");
            }

            return new QuadraticResult { Radical = radical, DecimalVal = decimalStr, Steps = steps.ToString() };
        }
    }
}

// ============================================================
// UNIVERSAL EQUATION SOLVER
// ============================================================
static class UniversalEquationSolver
{
    public static EquationResult Solve(string input)
    {
        string equation = Normalize(input);
        if (string.IsNullOrWhiteSpace(equation)) throw new Exception("Empty equation");
        if (!equation.Contains("=")) equation += "=0";

        string[] parts = equation.Split('=');
        if (parts.Length != 2) throw new Exception("Only one '=' is allowed");

        string left = parts[0];
        string right = parts[1];
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            throw new Exception("Missing equation side");

        try
        {
            ExprNode leftTree = ExpressionParser.Parse(left);
            ExprNode rightTree = ExpressionParser.Parse(right);

            if (PolynomialConverter.TryConvert(leftTree, out Poly leftPoly) &&
                PolynomialConverter.TryConvert(rightTree, out Poly rightPoly))
            {
                Poly polynomial = leftPoly - rightPoly;
                return SolvePolynomial(polynomial, equation);
            }
        }
        catch { }

        EquationResult? special = SpecialEquationSolver.TrySolve(left, right, equation);
        if (special != null) return special;

        return NumericalEquationSolver.Solve(left, right, equation);
    }

    static string Normalize(string value)
    {
        return value.Replace(" ", "").Replace("×", "*").Replace("÷", "/").Replace("−", "-")
            .Replace("π", "pi").Replace("Π", "pi").Replace("²", "^2").Replace("³", "^3").Replace(",", ".");
    }

    static EquationResult SolvePolynomial(Poly polynomial, string equation)
    {
        if (polynomial.C.All(x => Math.Abs(x) < 1e-12))
        {
            return new EquationResult
            {
                Result = "عدد لا نهائي من الحلول",
                Steps = $"المعادلة:\n{equation}\n\nبعد نقل الحدود:\n\n0 = 0\n\nإذن المعادلة صحيحة لكل x."
            };
        }

        int degree = polynomial.Degree;

        if (degree == 0)
        {
            return new EquationResult
            {
                Result = "لا يوجد حل",
                Steps = $"المعادلة:\n{equation}\n\nبعد التبسيط:\n{Format.Number(polynomial.C[0])} = 0\n\nوهذه العبارة غير صحيحة."
            };
        }

        if (degree == 1)
        {
            double b = polynomial.C[1];
            double c = polynomial.C[0];
            double x = -c / b;

            return new EquationResult
            {
                Result = $"x = {Format.Fraction(x)}",
                Steps = $"المعادلة:\n{equation}\n\nبعد التبسيط:\n{Format.Fraction(b)}x {Format.Signed(c)} = 0\n\nx = {Format.Fraction(x)}"
            };
        }

        if (degree == 2)
        {
            QuadraticResult quadratic = EquationSolver.SolveQuadratic(polynomial.C[2], polynomial.C[1], polynomial.C[0]);
            return new EquationResult
            {
                Result = quadratic.Radical,
                Steps = $"المعادلة:\n{equation}\n\n{quadratic.Steps}"
            };
        }

        return HigherDegreePolynomialSolver.Solve(polynomial, equation);
    }
}

// ============================================================
// SPECIAL EQUATION SOLVER
// ============================================================
static class SpecialEquationSolver
{
    // يبني خطوة "حل المعادلة الخطية الإضافية" فقط عندما تكون الوسيطة أكثر من x مجردة
    // (مثلاً ln(2x+1)=2 تحتاج خطوة إضافية بعد إيجاد قيمة 2x+1، بعكس ln(x)=2)
    static string LinearArgumentSteps(double coefficient, double constant, string innerText, double requiredValue, out double x)
    {
        x = (requiredValue - constant) / coefficient;
        bool trivial = Math.Abs(coefficient - 1) < 1e-9 && Math.Abs(constant) < 1e-9;
        if (trivial) return "";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"بما أن الدالة مطبقة على العبارة ({innerText}) وليس على x مباشرة، نحل الآن المعادلة الخطية الناتجة:");
        sb.AppendLine($"{Format.Fraction(coefficient)}x {Format.Signed(constant)} = {Format.Number(requiredValue)}");
        sb.AppendLine($"{Format.Fraction(coefficient)}x = {Format.Number(requiredValue - constant)}");
        sb.AppendLine($"x = {Format.Number(requiredValue - constant)} / {Format.Fraction(coefficient)}");
        sb.AppendLine($"x = {Format.Fraction(x)}");
        return sb.ToString();
    }

    public static EquationResult? TrySolve(string left, string right, string original)
    {
        // ===== a^x = b =====
        Match power = Regex.Match(left, @"^([+-]?[0-9]+(?:\.[0-9]+)?)\^x$", RegexOptions.IgnoreCase);

        if (power.Success)
        {
            if (TryEvaluate(power.Groups[1].Value, out double baseValue) && TryEvaluate(right, out double target))
            {
                if (baseValue > 0 && Math.Abs(baseValue - 1) > 1e-12 && target > 0)
                {
                    double x = Math.Log(target) / Math.Log(baseValue);
                    var s = new StringBuilder();
                    s.AppendLine($"المعادلة:\n{original}");
                    s.AppendLine();
                    s.AppendLine("١. مفهوم المعادلة الأسية");
                    s.AppendLine($"هذه معادلة أسية: الأساس ({Format.Number(baseValue)}) ثابت والمجهول x موجود في الأس. لإنزال x من الأس نأخذ اللوغاريتم الطبيعي (ln) للطرفين.");
                    s.AppendLine();
                    s.AppendLine("٢. خطوات الحل");
                    s.AppendLine($"المعادلة الأصلية: {Format.Number(baseValue)}^x = {Format.Number(target)}");
                    s.AppendLine("نأخذ ln للطرفين:");
                    s.AppendLine($"ln({Format.Number(baseValue)}^x) = ln({Format.Number(target)})");
                    s.AppendLine("بخاصية اللوغاريتمات ln(aᵇ) = b·ln(a) ينزل الأس كمعامل ضرب:");
                    s.AppendLine($"x · ln({Format.Number(baseValue)}) = ln({Format.Number(target)})");
                    s.AppendLine($"x = ln({Format.Number(target)}) / ln({Format.Number(baseValue)})");
                    s.AppendLine();
                    s.AppendLine("٣. النتيجة النهائية");
                    s.AppendLine($"الحل المضبوط (بالدلالة الرمزية): x = ln({Format.Number(target)}) / ln({Format.Number(baseValue)})");
                    s.AppendLine($"الحل التقريبي (بالأرقام): x ≈ {Format.Number(x)}");

                    return new EquationResult { Result = $"x ≈ {Format.Number(x)}", Steps = s.ToString() };
                }

                if (Math.Abs(baseValue - 1) < 1e-12)
                {
                    return target == 1
                        ? new EquationResult { Result = "عدد لا نهائي من الحلول", Steps = $"المعادلة:\n{original}\n\n1^x تساوي 1 لكل قيمة x، فالمعادلة صحيحة دائمًا." }
                        : new EquationResult { Result = "لا يوجد حل", Steps = $"المعادلة:\n{original}\n\n1^x تساوي 1 دائمًا، وهي لا تساوي {Format.Number(target)}، فلا يوجد حل." };
                }
            }
        }

        // ===== function(inner) = target =====
        Match functionMatch = Regex.Match(left, @"^(sin|cos|tan|asin|acos|atan|ln|log|exp|sqrt)(.*)$", RegexOptions.IgnoreCase);
        if (!functionMatch.Success) return null;

        string function = functionMatch.Groups[1].Value.ToLowerInvariant();
        string inner = functionMatch.Groups[2].Value;

        if (!TryEvaluate(right, out double targetValue)) return null;
        if (!TryGetLinear(inner, out double coefficient, out double constant)) return null;
        if (Math.Abs(coefficient) < 1e-12) return null;

        // ملاحظة: sin/cos/tan/asin/acos/atan تعمل هنا بالدرجات، متسقة مع FunctionNode
        string innerLabel = (inner.StartsWith("(") && inner.EndsWith(")")) ? inner[1..^1] : inner;

        // ===== ln =====
        if (function == "ln")
        {
            double required = Math.Exp(targetValue);
            if (!double.IsFinite(required)) return null;

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم اللوغاريتم الطبيعي (ln)");
            s.AppendLine("الدالة ln هي اللوغاريتم الطبيعي، وهي الدالة العكسية للدالة الأسية ذات الأساس e (العدد النيبيري، وقيمته التقريبية 2.718).");
            s.AppendLine("لإلغاء ln نطبّق الدالة الأسية e على الطرفين، لأن e و ln يُلغيان بعضهما البعض.");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: ln({innerLabel}) = {Format.Number(targetValue)}");
            s.AppendLine("نطبّق الدالة الأسية e على الطرفين:");
            s.AppendLine($"e^(ln({innerLabel})) = e^{Format.Number(targetValue)}");
            s.AppendLine("بما أن e و ln يلغيان بعضهما، يتبسط الطرف الأيسر إلى:");
            s.AppendLine($"{innerLabel} = e^{Format.Number(targetValue)} ≈ {Format.Number(required)}");

            string linStepsLn = LinearArgumentSteps(coefficient, constant, innerLabel, required, out double xLn);
            if (!string.IsNullOrEmpty(linStepsLn)) s.Append(linStepsLn);

            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            bool trivialLn = Math.Abs(coefficient - 1) < 1e-9 && Math.Abs(constant) < 1e-9;
            if (trivialLn)
            {
                s.AppendLine($"الحل المضبوط (بالدلالة الرمزية): x = e^{Format.Number(targetValue)}");
                s.AppendLine($"الحل التقريبي (بالأرقام): x ≈ {Format.Number(xLn)}");
            }
            else
            {
                s.AppendLine($"الحل: x = {Format.Fraction(xLn)}");
                s.AppendLine($"(القيمة التقريبية: x ≈ {Format.Number(xLn)})");
            }
            s.AppendLine();
            s.AppendLine("ملاحظة: يشترط أن يكون ما بداخل ln موجبًا (> 0)، لأن هذا هو مجال تعريف اللوغاريتم، والقيمة الموجودة هنا موجبة بالفعل.");

            return new EquationResult { Result = $"x ≈ {Format.Number(xLn)}", Steps = s.ToString() };
        }

        // ===== log (أساس 10) =====
        if (function == "log")
        {
            double required = Math.Pow(10, targetValue);
            if (!double.IsFinite(required)) return null;

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم اللوغاريتم العشري (log)");
            s.AppendLine("الدالة log هنا هي اللوغاريتم العشري (أساسه 10)، وهي الدالة العكسية للدالة الأسية ذات الأساس 10.");
            s.AppendLine("لإلغاء log نطبّق الأس 10 على الطرفين، لأن 10 وlog يُلغيان بعضهما البعض.");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: log({innerLabel}) = {Format.Number(targetValue)}");
            s.AppendLine("نرفع 10 لأس الطرفين:");
            s.AppendLine($"10^(log({innerLabel})) = 10^{Format.Number(targetValue)}");
            s.AppendLine($"{innerLabel} = 10^{Format.Number(targetValue)} = {Format.Number(required)}");

            string linStepsLog = LinearArgumentSteps(coefficient, constant, innerLabel, required, out double xLog);
            if (!string.IsNullOrEmpty(linStepsLog)) s.Append(linStepsLog);

            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            bool trivialLog = Math.Abs(coefficient - 1) < 1e-9 && Math.Abs(constant) < 1e-9;
            if (trivialLog)
                s.AppendLine($"الحل المضبوط: x = 10^{Format.Number(targetValue)} = {Format.Number(required)}");
            else
                s.AppendLine($"الحل: x = {Format.Fraction(xLog)}");
            s.AppendLine($"(القيمة التقريبية: x ≈ {Format.Number(xLog)})");
            s.AppendLine();
            s.AppendLine("ملاحظة: يشترط أن يكون ما بداخل log موجبًا (> 0)، لأن هذا هو مجال تعريف اللوغاريتم.");

            return new EquationResult { Result = $"x ≈ {Format.Number(xLog)}", Steps = s.ToString() };
        }

        // ===== exp =====
        if (function == "exp")
        {
            if (targetValue <= 0)
                return new EquationResult { Result = "لا يوجد حل حقيقي", Steps = $"المعادلة:\n{original}\n\nexp(x) = eˣ قيمتها موجبة دائمًا مهما كانت x، فلا يمكن أن تساوي {Format.Number(targetValue)}." };

            double required = Math.Log(targetValue);

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم الدالة الأسية exp");
            s.AppendLine("الدالة exp(y) = eʸ هي الدالة الأسية بالأساس e، ودالتها العكسية هي ln.");
            s.AppendLine("لإلغاء exp نطبّق ln على الطرفين.");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: exp({innerLabel}) = {Format.Number(targetValue)}");
            s.AppendLine("نطبّق ln على الطرفين:");
            s.AppendLine($"ln(exp({innerLabel})) = ln({Format.Number(targetValue)})");
            s.AppendLine($"{innerLabel} = ln({Format.Number(targetValue)}) ≈ {Format.Number(required)}");

            string linStepsExp = LinearArgumentSteps(coefficient, constant, innerLabel, required, out double xExp);
            if (!string.IsNullOrEmpty(linStepsExp)) s.Append(linStepsExp);

            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            bool trivialExp = Math.Abs(coefficient - 1) < 1e-9 && Math.Abs(constant) < 1e-9;
            if (trivialExp)
                s.AppendLine($"الحل المضبوط (بالدلالة الرمزية): x = ln({Format.Number(targetValue)})");
            else
                s.AppendLine($"الحل: x = {Format.Fraction(xExp)}");
            s.AppendLine($"(القيمة التقريبية: x ≈ {Format.Number(xExp)})");

            return new EquationResult { Result = $"x ≈ {Format.Number(xExp)}", Steps = s.ToString() };
        }

        // ===== sqrt =====
        if (function == "sqrt")
        {
            if (targetValue < 0)
                return new EquationResult { Result = "لا يوجد حل حقيقي", Steps = $"المعادلة:\n{original}\n\nالجذر التربيعي لا يكون سالبًا أبدًا، فلا يوجد حل." };

            double required = targetValue * targetValue;

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم الجذر التربيعي");
            s.AppendLine("الجذر التربيعي √y هو العدد غير السالب الذي مربعه يساوي y (بشرط y ≥ 0). لإلغاء الجذر نربّع الطرفين.");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: √({innerLabel}) = {Format.Number(targetValue)}");
            s.AppendLine("نربّع الطرفين:");
            s.AppendLine($"(√({innerLabel}))² = ({Format.Number(targetValue)})²");
            s.AppendLine($"{innerLabel} = {Format.Number(required)}");

            string linStepsSqrt = LinearArgumentSteps(coefficient, constant, innerLabel, required, out double xSqrt);
            if (!string.IsNullOrEmpty(linStepsSqrt)) s.Append(linStepsSqrt);

            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            s.AppendLine($"الحل: x = {Format.Fraction(xSqrt)}");
            s.AppendLine();
            s.AppendLine($"ملاحظة: هذا الحل صحيح فقط إذا كان الطرف الأيمن الأصلي ({Format.Number(targetValue)}) غير سالب، وهذا الشرط متحقق هنا.");

            return new EquationResult { Result = $"x = {Format.Fraction(xSqrt)}", Steps = s.ToString() };
        }

        // ===== sin =====
        if (function == "sin")
        {
            if (targetValue < -1 || targetValue > 1)
                return new EquationResult { Result = "لا يوجد حل حقيقي", Steps = $"المعادلة:\n{original}\n\nقيم دالة sin تتراوح دائمًا بين -1 و1، وبما أن {Format.Number(targetValue)} خارج هذا المجال فلا يوجد حل حقيقي." };

            double angle = Math.Asin(targetValue) * 180 / Math.PI;
            double x1 = (angle - constant) / coefficient;
            double x2 = (180 - angle - constant) / coefficient;

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم دالة الجيب (sin)");
            s.AppendLine("قيمة sin(الزاوية) تتراوح دائمًا بين -1 و1. لإيجاد الزاوية من قيمة sin نستخدم الدالة العكسية arcsin (تُكتب أيضًا sin⁻¹).");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: sin({innerLabel}) = {Format.Number(targetValue)}");
            s.AppendLine("نطبّق arcsin على الطرفين:");
            s.AppendLine($"{innerLabel} = arcsin({Format.Number(targetValue)}) ≈ {Format.Number(angle)}°");
            s.AppendLine($"وبسبب دورية دالة الجيب، يوجد حل ثانٍ داخل نفس الدورة: {innerLabel} ≈ {Format.Number(180 - angle)}°");
            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            s.AppendLine($"x₁ ≈ {Format.Number(x1)}");
            s.AppendLine($"x₂ ≈ {Format.Number(x2)}");
            s.AppendLine();
            s.AppendLine("ملاحظة: بسبب دورية دالة sin (تتكرر كل 360°)، توجد حلول أخرى كثيرة تختلف عن x₁ وx₂ بمضاعفات صحيحة للدورة.");

            return new EquationResult { Result = $"x₁ ≈ {Format.Number(x1)}\nx₂ ≈ {Format.Number(x2)}", Steps = s.ToString() };
        }

        // ===== cos =====
        if (function == "cos")
        {
            if (targetValue < -1 || targetValue > 1)
                return new EquationResult { Result = "لا يوجد حل حقيقي", Steps = $"المعادلة:\n{original}\n\nقيم دالة cos تتراوح دائمًا بين -1 و1، فلا يوجد حل حقيقي." };

            double angle = Math.Acos(targetValue) * 180 / Math.PI;
            double x1 = (angle - constant) / coefficient;
            double x2 = (-angle - constant) / coefficient;

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم دالة جيب التمام (cos)");
            s.AppendLine("قيمة cos(الزاوية) تتراوح دائمًا بين -1 و1. لإيجاد الزاوية من قيمة cos نستخدم الدالة العكسية arccos (تُكتب أيضًا cos⁻¹).");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: cos({innerLabel}) = {Format.Number(targetValue)}");
            s.AppendLine("نطبّق arccos على الطرفين:");
            s.AppendLine($"{innerLabel} = ±arccos({Format.Number(targetValue)}) ≈ ±{Format.Number(angle)}°");
            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            s.AppendLine($"x₁ ≈ {Format.Number(x1)}");
            s.AppendLine($"x₂ ≈ {Format.Number(x2)}");
            s.AppendLine();
            s.AppendLine("ملاحظة: بسبب دورية دالة cos (تتكرر كل 360°)، توجد حلول أخرى تختلف عن x₁ وx₂ بمضاعفات صحيحة للدورة.");

            return new EquationResult { Result = $"x₁ ≈ {Format.Number(x1)}\nx₂ ≈ {Format.Number(x2)}", Steps = s.ToString() };
        }

        // ===== tan =====
        if (function == "tan")
        {
            double angle = Math.Atan(targetValue) * 180 / Math.PI;
            double x = (angle - constant) / coefficient;

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم دالة الظل (tan)");
            s.AppendLine("الدالة tan(الزاوية) = sin/cos، وقيمتها يمكن أن تكون أي عدد حقيقي. الدالة العكسية arctan (تُكتب أيضًا tan⁻¹) تعطي الزاوية من قيمة tan.");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: tan({innerLabel}) = {Format.Number(targetValue)}");
            s.AppendLine("نطبّق arctan على الطرفين:");
            s.AppendLine($"{innerLabel} = arctan({Format.Number(targetValue)}) ≈ {Format.Number(angle)}°");
            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            s.AppendLine($"x ≈ {Format.Number(x)}");
            s.AppendLine();
            s.AppendLine("ملاحظة: بسبب دورية دالة tan (تتكرر كل 180°)، توجد حلول أخرى كثيرة.");

            return new EquationResult { Result = $"x ≈ {Format.Number(x)}", Steps = s.ToString() };
        }

        // ===== asin =====
        if (function == "asin")
        {
            if (targetValue < -90 || targetValue > 90)
                return new EquationResult { Result = "لا يوجد حل حقيقي", Steps = $"المعادلة:\n{original}\n\nالمجال الأساسي لدالة arcsin هو من -90° إلى 90°، فلا يوجد حل حقيقي." };

            double required = Math.Sin(targetValue * Math.PI / 180);

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم arcsin");
            s.AppendLine("الدالة arcsin(y) تعطي الزاوية (بين -90° و90°) التي جيبها يساوي y. لإلغاء arcsin نطبّق sin على الطرفين.");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: arcsin({innerLabel}) = {Format.Number(targetValue)}°");
            s.AppendLine("نطبّق sin على الطرفين:");
            s.AppendLine($"{innerLabel} = sin({Format.Number(targetValue)}°) ≈ {Format.Number(required)}");

            string linStepsAsin = LinearArgumentSteps(coefficient, constant, innerLabel, required, out double xAsin);
            if (!string.IsNullOrEmpty(linStepsAsin)) s.Append(linStepsAsin);

            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            s.AppendLine($"x ≈ {Format.Number(xAsin)}");

            return new EquationResult { Result = $"x ≈ {Format.Number(xAsin)}", Steps = s.ToString() };
        }

        // ===== acos =====
        if (function == "acos")
        {
            if (targetValue < 0 || targetValue > 180)
                return new EquationResult { Result = "لا يوجد حل حقيقي", Steps = $"المعادلة:\n{original}\n\nالمجال الأساسي لدالة arccos هو من 0° إلى 180°، فلا يوجد حل حقيقي." };

            double required = Math.Cos(targetValue * Math.PI / 180);

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم arccos");
            s.AppendLine("الدالة arccos(y) تعطي الزاوية (بين 0° و180°) التي جيب تمامها يساوي y. لإلغاء arccos نطبّق cos على الطرفين.");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: arccos({innerLabel}) = {Format.Number(targetValue)}°");
            s.AppendLine("نطبّق cos على الطرفين:");
            s.AppendLine($"{innerLabel} = cos({Format.Number(targetValue)}°) ≈ {Format.Number(required)}");

            string linStepsAcos = LinearArgumentSteps(coefficient, constant, innerLabel, required, out double xAcos);
            if (!string.IsNullOrEmpty(linStepsAcos)) s.Append(linStepsAcos);

            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            s.AppendLine($"x ≈ {Format.Number(xAcos)}");

            return new EquationResult { Result = $"x ≈ {Format.Number(xAcos)}", Steps = s.ToString() };
        }

        // ===== atan =====
        if (function == "atan")
        {
            double required = Math.Tan(targetValue * Math.PI / 180);

            var s = new StringBuilder();
            s.AppendLine($"المعادلة:\n{original}");
            s.AppendLine();
            s.AppendLine("١. مفهوم arctan");
            s.AppendLine("الدالة arctan(y) تعطي الزاوية (بين -90° و90°) التي ظلها يساوي y. لإلغاء arctan نطبّق tan على الطرفين.");
            s.AppendLine();
            s.AppendLine("٢. خطوات الحل");
            s.AppendLine($"المعادلة الأصلية: arctan({innerLabel}) = {Format.Number(targetValue)}°");
            s.AppendLine("نطبّق tan على الطرفين:");
            s.AppendLine($"{innerLabel} = tan({Format.Number(targetValue)}°) ≈ {Format.Number(required)}");

            string linStepsAtan = LinearArgumentSteps(coefficient, constant, innerLabel, required, out double xAtan);
            if (!string.IsNullOrEmpty(linStepsAtan)) s.Append(linStepsAtan);

            s.AppendLine();
            s.AppendLine("٣. النتيجة النهائية");
            s.AppendLine($"x ≈ {Format.Number(xAtan)}");

            return new EquationResult { Result = $"x ≈ {Format.Number(xAtan)}", Steps = s.ToString() };
        }

        return null;
    }

    static bool TryEvaluate(string expression, out double value)
    {
        value = 0;
        try { value = ExpressionEvaluator.Evaluate(expression); return double.IsFinite(value); }
        catch { return false; }
    }

    static bool TryGetLinear(string expression, out double coefficient, out double constant)
    {
        coefficient = 0; constant = 0;
        try
        {
            ExprNode tree = ExpressionParser.Parse(expression);
            if (!PolynomialConverter.TryConvert(tree, out Poly poly)) return false;
            if (poly.Degree > 1) return false;

            coefficient = poly.C.Length > 1 ? poly.C[1] : 0;
            constant = poly.C.Length > 0 ? poly.C[0] : 0;
            return true;
        }
        catch { return false; }
    }
}

// ============================================================
// HIGH DEGREE POLYNOMIAL SOLVER
// ============================================================
static class HigherDegreePolynomialSolver
{
    public static EquationResult Solve(Poly polynomial, string equation)
    {
        var steps = new StringBuilder();
        steps.AppendLine($"المعادلة:\n{equation}");
        steps.AppendLine();
        steps.AppendLine($"هذه معادلة من الدرجة {polynomial.Degree}.");
        steps.AppendLine();
        steps.AppendLine("── الطريقة الأولى: نظرية الجذر النسبي والقسمة التركيبية ──");

        double[] remaining = polynomial.C.ToArray();
        List<double> roots = new();

        if (AllInteger(remaining))
        {
            while (remaining.Length > 2)
            {
                List<double> candidates = RationalCandidates(remaining);
                if (candidates.Count > 0)
                    steps.AppendLine($"الجذور المرشحة: {string.Join(", ", candidates.Select(Format.Fraction))}");

                bool found = false;
                foreach (double candidate in candidates)
                {
                    double remainder = Evaluate(remaining, candidate);
                    if (Math.Abs(remainder) < 1e-7)
                    {
                        roots.Add(candidate);
                        double[] next = SyntheticDivide(remaining, candidate);

                        steps.AppendLine();
                        steps.AppendLine($"اختبار x = {Format.Fraction(candidate)} بالقسمة التركيبية → الباقي ≈ 0 ✓ (جذر صحيح)");
                        steps.AppendLine($"كثيرة الحدود بعد القسمة (من أعلى درجة للثابت): {string.Join(" , ", next.Reverse().Select(Format.Fraction))}");
                        steps.AppendLine();

                        remaining = next;
                        found = true;
                        break;
                    }
                }
                if (!found) break;
            }

            if (roots.Count == 0)
                steps.AppendLine("لم يتم إيجاد أي جذر نسبي من بين المرشحين.");
        }
        else
        {
            steps.AppendLine("المعاملات ليست كلها أعدادًا صحيحة، فهذه الطريقة غير قابلة للتطبيق مباشرة.");
        }

        steps.AppendLine();

        if (remaining.Length == 2)
        {
            double x = -remaining[0] / remaining[1];
            if (double.IsFinite(x)) roots.Add(x);

            steps.AppendLine($"الجزء المتبقي خطي: {Format.Fraction(remaining[1])}x {Format.Signed(remaining[0])} = 0");
            steps.AppendLine($"الجذر المتبقي: x = {Format.Fraction(x)}");
        }
        else if (remaining.Length == 3)
        {
            steps.AppendLine("تبقت معادلة تربيعية، نحلها بنفس طريقة المعادلة التربيعية:");
            QuadraticResult q = EquationSolver.SolveQuadratic(remaining[2], remaining[1], remaining[0]);
            steps.AppendLine(q.Steps);
            AddQuadraticRealRoots(remaining, roots);
        }
        else if (remaining.Length > 3)
        {
            steps.AppendLine();
            steps.AppendLine("── الطريقة الثانية: الطريقة العددية (Durand-Kerner) ──");
            steps.AppendLine("طريقة تكرارية تبدأ بتخمينات أولية للجذور المتبقية وتحسّنها تدريجيًا حتى تستقر، وتغطي كل الحالات.");

            Complex[] numerical = FindRoots(remaining);
            foreach (Complex root in numerical)
                if (Math.Abs(root.Imaginary) < 1e-7 && double.IsFinite(root.Real))
                    roots.Add(root.Real);
        }

        roots = roots.Where(double.IsFinite).OrderBy(x => x)
            .GroupBy(x => Math.Round(x, 7)).Select(g => g.First()).ToList();

        string result = roots.Count == 0
            ? "لم يتم العثور على حلول حقيقية."
            : string.Join("\n", roots.Select((x, i) => $"x{i + 1} = {Format.Number(x)}"));

        steps.AppendLine();
        steps.AppendLine("النتيجة النهائية:");
        steps.AppendLine(result);

        return new EquationResult { Result = result, Steps = steps.ToString() };
    }

    static void AddQuadraticRealRoots(double[] coefficients, List<double> roots)
    {
        double a = coefficients[2], b = coefficients[1], c = coefficients[0];
        double delta = b * b - 4 * a * c;
        if (delta < -1e-10) return;
        if (Math.Abs(delta) < 1e-12) delta = 0;

        if (delta == 0)
        {
            double x = -b / (2 * a);
            if (double.IsFinite(x)) roots.Add(x);
            return;
        }

        double sqrt = Math.Sqrt(delta);
        double x1 = (-b + sqrt) / (2 * a);
        double x2 = (-b - sqrt) / (2 * a);
        if (double.IsFinite(x1)) roots.Add(x1);
        if (double.IsFinite(x2)) roots.Add(x2);
    }

    static bool AllInteger(double[] coefficients) =>
        coefficients.All(x => double.IsFinite(x) && Math.Abs(x - Math.Round(x)) < 1e-8);

    static List<double> RationalCandidates(double[] coefficients)
    {
        long leading = Math.Abs((long)Math.Round(coefficients[^1]));
        long constant = Math.Abs((long)Math.Round(coefficients[0]));
        if (leading == 0) return new();
        if (constant == 0) return new() { 0 };

        List<long> numerators = Divisors(constant);
        List<long> denominators = Divisors(leading);

        return numerators.SelectMany(n => denominators.Select(d => (double)n / d))
            .SelectMany(x => new[] { x, -x }).Distinct().ToList();
    }

    static List<long> Divisors(long number)
    {
        List<long> result = new();
        if (number <= 0) return result;
        for (long i = 1; i <= number / i; i++)
        {
            if (number % i == 0) { result.Add(i); if (i != number / i) result.Add(number / i); }
        }
        return result;
    }

    static double Evaluate(double[] coefficients, double x)
    {
        double result = 0;
        for (int i = coefficients.Length - 1; i >= 0; i--) result = result * x + coefficients[i];
        return result;
    }

    static double[] SyntheticDivide(double[] coefficients, double root)
    {
        int degree = coefficients.Length - 1;
        double[] result = new double[degree];
        result[degree - 1] = coefficients[degree];
        for (int i = degree - 2; i >= 0; i--) result[i] = coefficients[i + 1] + root * result[i + 1];
        return result;
    }

    static Complex[] FindRoots(double[] coefficients)
    {
        int degree = coefficients.Length - 1;
        if (degree <= 0) return Array.Empty<Complex>();

        double leading = coefficients[^1];
        if (Math.Abs(leading) < 1e-15) return Array.Empty<Complex>();

        Complex[] roots = new Complex[degree];
        double radius = 1 + coefficients.Take(degree).Select(Math.Abs).DefaultIfEmpty(0).Max() / Math.Abs(leading);

        for (int i = 0; i < degree; i++)
        {
            double angle = 2 * Math.PI * i / degree;
            roots[i] = Complex.FromPolarCoordinates(radius, angle);
        }

        for (int iteration = 0; iteration < 3000; iteration++)
        {
            double maxChange = 0;
            for (int i = 0; i < degree; i++)
            {
                Complex value = EvaluateComplex(coefficients, roots[i]);
                Complex denominator = Complex.One;
                for (int j = 0; j < degree; j++) if (i != j) denominator *= (roots[i] - roots[j]);
                if (denominator.Magnitude < 1e-20) continue;

                Complex change = value / denominator;
                roots[i] -= change;
                maxChange = Math.Max(maxChange, change.Magnitude);
            }
            if (maxChange < 1e-12) break;
        }

        return roots;
    }

    static Complex EvaluateComplex(double[] coefficients, Complex x)
    {
        Complex result = 0;
        for (int i = coefficients.Length - 1; i >= 0; i--) result = result * x + coefficients[i];
        return result;
    }
}

// ============================================================
// NUMERICAL EQUATION SOLVER
// ============================================================
static class NumericalEquationSolver
{
    public static EquationResult Solve(string left, string right, string original)
    {
        ExprNode leftTree = ExpressionParser.Parse(left);
        ExprNode rightTree = ExpressionParser.Parse(right);

        double Function(double x)
        {
            try
            {
                double a = leftTree.Evaluate(x);
                double b = rightTree.Evaluate(x);
                if (!double.IsFinite(a) || !double.IsFinite(b)) return double.NaN;
                return a - b;
            }
            catch { return double.NaN; }
        }

        List<double> roots = new();
        const double min = -100, max = 100, step = 0.05;

        double previousX = min;
        double previousY = Function(previousX);
        int iterations = (int)Math.Round((max - min) / step);

        for (int i = 1; i <= iterations; i++)
        {
            double currentX = min + i * step;
            double currentY = Function(currentX);

            if (double.IsFinite(previousY) && double.IsFinite(currentY))
            {
                if (Math.Abs(currentY) < 1e-7)
                {
                    double refined = Newton(Function, currentX);
                    if (IsValidRoot(Function, refined)) roots.Add(refined);
                    else if (Math.Abs(currentY) < 1e-10) roots.Add(currentX);
                }
                else if (previousY * currentY < 0)
                {
                    double root = Bisection(Function, previousX, currentX);
                    if (IsValidRoot(Function, root)) roots.Add(root);
                }
            }

            previousX = currentX;
            previousY = currentY;
        }

        for (double seed = min; seed <= max; seed += 1.0)
        {
            double root = Newton(Function, seed);
            if (IsValidRoot(Function, root)) roots.Add(root);
        }

        roots = roots.Where(x => double.IsFinite(x)).Where(x => x >= min && x <= max)
            .Where(x => Math.Abs(Function(x)) < 1e-8).OrderBy(x => x)
            .GroupBy(x => Math.Round(x, 7)).Select(g => g.First()).ToList();

        var steps = new StringBuilder();
        steps.AppendLine($"المعادلة:\n{original}");
        steps.AppendLine();
        steps.AppendLine("نستخدم الحل العددي.");
        steps.AppendLine();
        steps.AppendLine("نعرّف:");
        steps.AppendLine("f(x) = الطرف الأيسر − الطرف الأيمن");
        steps.AppendLine();
        steps.AppendLine("نبحث عن:");
        steps.AppendLine("f(x) ≈ 0");
        steps.AppendLine();
        steps.AppendLine("نطاق البحث:");
        steps.AppendLine("-100 ≤ x ≤ 100");
        steps.AppendLine();

        if (roots.Count == 0)
        {
            steps.AppendLine("لم يتم العثور على حل حقيقي ضمن نطاق البحث.");
            return new EquationResult { Result = "لا يوجد حل حقيقي ضمن النطاق المحدد.", Steps = steps.ToString() };
        }

        steps.AppendLine("الحلول التقريبية:");
        steps.AppendLine();

        foreach (double root in roots)
        {
            double residual = Math.Abs(Function(root));
            steps.AppendLine($"x ≈ {Format.Number(root)}");
            steps.AppendLine($"الباقي العددي |f(x)| ≈ {Format.Number(residual)}");
            steps.AppendLine();
        }

        return new EquationResult
        {
            Result = string.Join("\n", roots.Select((x, i) => $"x{i + 1} ≈ {Format.Number(x)}")),
            Steps = steps.ToString()
        };
    }

    static double Bisection(Func<double, double> function, double a, double b)
    {
        double fa = function(a), fb = function(b);
        if (!double.IsFinite(fa) || !double.IsFinite(fb)) return double.NaN;
        if (Math.Abs(fa) < 1e-14) return a;
        if (Math.Abs(fb) < 1e-14) return b;
        if (fa * fb > 0) return double.NaN;

        for (int i = 0; i < 200; i++)
        {
            double middle = a + (b - a) / 2.0;
            double fm = function(middle);
            if (!double.IsFinite(fm)) return double.NaN;

            if (Math.Abs(fm) < 1e-12 || Math.Abs(b - a) < 1e-12) return middle;

            if (fa * fm <= 0) { b = middle; fb = fm; }
            else { a = middle; fa = fm; }
        }

        double result = a + (b - a) / 2.0;
        return IsValidRoot(function, result) ? result : double.NaN;
    }

    static double Newton(Func<double, double> function, double initial)
    {
        double x = initial;

        for (int i = 0; i < 100; i++)
        {
            double y = function(x);
            if (!double.IsFinite(y)) return double.NaN;
            if (Math.Abs(y) < 1e-12) return x;

            double h = 1e-6 * Math.Max(1.0, Math.Abs(x));
            double y1 = function(x + h), y2 = function(x - h);
            if (!double.IsFinite(y1) || !double.IsFinite(y2)) return double.NaN;

            double derivative = (y1 - y2) / (2.0 * h);
            if (!double.IsFinite(derivative) || Math.Abs(derivative) < 1e-14) return double.NaN;

            double next = x - y / derivative;
            if (!double.IsFinite(next)) return double.NaN;
            if (Math.Abs(next) > 1e6) return double.NaN;

            if (Math.Abs(next - x) < 1e-12)
            {
                double finalY = function(next);
                return double.IsFinite(finalY) && Math.Abs(finalY) < 1e-10 ? next : double.NaN;
            }

            x = next;
        }

        double finalValue = function(x);
        return double.IsFinite(finalValue) && Math.Abs(finalValue) < 1e-10 ? x : double.NaN;
    }

    static bool IsValidRoot(Func<double, double> function, double root)
    {
        if (!double.IsFinite(root)) return false;
        if (root < -100 || root > 100) return false;
        double value = function(root);
        return double.IsFinite(value) && Math.Abs(value) < 1e-8;
    }
}

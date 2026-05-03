using Input = System.ReadOnlySpan<System.Char>;

namespace CsvParser;

public readonly struct ParseError;
public sealed class ParseException(ParseError error) : Exception
{
    public ParseError Error { get; } = error;
}

public sealed class UninitializedInstanceException() : Exception("Instance uninitialized");

public interface IRule
{
    Input Text { get; }
    void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct;
    void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct;
}

public interface IVisitor
{
    void Visit<T>(in T rule) where T : IRule, allows ref struct;
}

// EscapedQuote -> '\"' '\"'
public readonly ref struct EscapedQuote : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public EscapedQuote(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var i = 0;
        if (i >= input.Length || input[i] != '\"')
            throw new ParseException(new ParseError());
        i++;
        if (i >= input.Length || input[i] != '\"')
            throw new ParseException(new ParseError());
        i++;
        Length = i;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// NonQuoteChar -> [^\"]
public readonly ref struct NonQuoteChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public NonQuoteChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || !IsNot22Char(input[0]))
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsNot22Char(Char c) =>
        !(c == '\"');
}

// QuotedChar
//     EscapedQuote
//     NonQuoteChar
public readonly ref struct QuotedChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public QuotedChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var escapedQuote = new EscapedQuote(input);
            index = 1;
            Length = escapedQuote.Length;
        }
        catch (ParseException)
        {
            var nonQuoteChar = new NonQuoteChar(input);
            index = 2;
            Length = nonQuoteChar.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : CsvParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new EscapedQuote(input, input, 3)); break;
            case 2: visitor.Visit(new NonQuoteChar(input, input, 3)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new EscapedQuote(input)); break;
            case 2: visitor.Visit(new NonQuoteChar(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : CsvParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in EscapedQuote escapedQuote);
        void Visit(in NonQuoteChar nonQuoteChar);
    }
}

// QuotedField -> '\"' QuotedChar* '\"'
public readonly ref struct QuotedField : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public QuotedField(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '\"')
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        while (true)
        {
            try
            {
                var nextQuotedChar = new QuotedChar(input[pos..]);
                pos += nextQuotedChar.Length;
            }
            catch (ParseException)
            {
                break;
            }
        }
        pos += SkipWhitespace(input[pos..]);
        if (pos >= input.Length || input[pos] != '\"')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var pos = 0;
        while (pos < Length)
        {
            try
            {
                var nextQuotedChar = new QuotedChar(input[pos..], input, 4);
                visitor.Visit(nextQuotedChar);
                pos += nextQuotedChar.Length;
            }
            catch (ParseException)
            {
                break;
            }
        }
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// SimpleField -> [^,\n\r\"]
public readonly ref struct SimpleField : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public SimpleField(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || !IsNot2C0A0D22Char(input[0]))
            throw new ParseException(new ParseError());
        var i = 1;
        while (i < input.Length && IsNot2C0A0D22Char(input[i]))
            i++;
        Length = i;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsNot2C0A0D22Char(Char c) =>
        !(c == ',' || c == '\n' || c == '\r' || c == '\"');
}

// Field
//     QuotedField
//     SimpleField
public readonly ref struct Field : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public Field(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var quotedField = new QuotedField(input);
            index = 1;
            Length = quotedField.Length;
        }
        catch (ParseException)
        {
            var simpleField = new SimpleField(input);
            index = 2;
            Length = simpleField.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : CsvParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new QuotedField(input, input, 6)); break;
            case 2: visitor.Visit(new SimpleField(input, input, 6)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new QuotedField(input)); break;
            case 2: visitor.Visit(new SimpleField(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : CsvParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in QuotedField quotedField);
        void Visit(in SimpleField simpleField);
    }
}

// Row -> Field (',' Field)* '\n'
public readonly ref struct Row : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 fieldStart;
    public Int32 Length { get; }

    public Row(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        fieldStart = pos;
        var field = new Field(input[pos..]);
        pos += field.Length;
        pos += SkipWhitespace(input[pos..]);
        while (pos < input.Length && input[pos] == ',')
        {
            pos += 1;
            var next = new Field(input[pos..]);
            pos += next.Length;
        }
        if (pos >= input.Length || input[pos] != '\n')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Field Field => new(input[fieldStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var field = new Field(input[fieldStart..], input, 7);
        visitor.Visit(field);
        var pos = fieldStart + field.Length;
        while (pos < Length)
        {
            try
            {
                pos += SkipWhitespace(input[pos..]);
                if (pos >= input.Length || input[pos] != ',') break;
                pos += 1;
                var nextField = new Field(input[pos..], input, 7);
                visitor.Visit(nextField);
                pos += nextField.Length;
                pos += SkipWhitespace(input[pos..]);
            }
            catch (ParseException) { break; }
        }
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// File -> Row+
public readonly ref struct File : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public File(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        var firstRow = new Row(input[pos..]);
        pos += firstRow.Length;
        while (true)
        {
            try
            {
                var nextRow = new Row(input[pos..]);
                pos += nextRow.Length;
            }
            catch (ParseException)
            {
                break;
            }
        }
        Length = pos;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var pos = 0;
        while (pos < Length)
        {
            try
            {
                var nextRow = new Row(input[pos..], input, 8);
                visitor.Visit(nextRow);
                pos += nextRow.Length;
            }
            catch (ParseException)
            {
                break;
            }
        }
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

public static class Rules
{
    public static void Visit<T>(Byte kind, Input input, ref T visitor) where T : IVisitor, allows ref struct
    {
        switch (kind)
        {
            case 0: break;
            case 1: visitor.Visit(new EscapedQuote(input)); break;
            case 2: visitor.Visit(new NonQuoteChar(input)); break;
            case 3: visitor.Visit(new QuotedChar(input)); break;
            case 4: visitor.Visit(new QuotedField(input)); break;
            case 5: visitor.Visit(new SimpleField(input)); break;
            case 6: visitor.Visit(new Field(input)); break;
            case 7: visitor.Visit(new Row(input)); break;
            case 8: visitor.Visit(new File(input)); break;
        }
    }
}

// General-purpose tree printer
public struct TreePrinter(Int32 depth = 0) : IVisitor
{
    public void Visit<T>(in T rule) where T : IRule, allows ref struct
    {
        var indent = new String(' ', depth * 2);
        var checker = new ChildChecker();
        rule.VisitChildren(ref checker);

        if (checker.HasChildren)
        {
            Console.WriteLine($"{indent}{typeof(T).Name}");
            var childPrinter = new TreePrinter(depth + 1);
            rule.VisitChildren(ref childPrinter);
        }
        else
        {
            Console.WriteLine($"{indent}{typeof(T).Name}: {rule.Text}");
        }
    }
}

public struct ChildChecker : IVisitor
{
    public Boolean HasChildren { get; private set; }

    public void Visit<T>(in T rule) where T : IRule, allows ref struct
    {
        HasChildren = true;
    }
}

public struct AncestorPrinter(List<String> ancestors, Int32 depth) : IVisitor
{
    public AncestorPrinter() : this(new List<String>(), 0) { }

    public void Visit<T>(in T rule) where T : IRule, allows ref struct
    {
        var name = typeof(T).Name;
        ancestors.Add(name);

        var indent = new String(' ', depth * 2);
        var checker = new ChildChecker();
        rule.VisitChildren(ref checker);

        if (checker.HasChildren)
        {
            Console.WriteLine($"{indent}{name}  [{String.Join(" > ", ancestors)}]");
            var child = new AncestorPrinter(ancestors, depth + 1);
            rule.VisitChildren(ref child);
        }
        else
        {
            Console.WriteLine($"{indent}{name}: {rule.Text}  [{String.Join(" > ", ancestors)}]");
        }

        ancestors.RemoveAt(ancestors.Count - 1);
    }
}

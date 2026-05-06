using Input = System.ReadOnlySpan<System.Char>;

namespace SfParser;

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

// Letter -> [\p{Alpha}]
public readonly ref struct Letter : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public Letter(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || !IsAlphaChar(input[0]))
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsAlphaChar(Char c) =>
        Char.IsLetter(c);
}

// OtherLetter -> [\p{General_Category=Other_Letter}]
public readonly ref struct OtherLetter : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public OtherLetter(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || !IsGeneralCategoryOtherLetterChar(input[0]))
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsGeneralCategoryOtherLetterChar(Char c) =>
        (Char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherLetter);
}

// NameStart
//     Letter
//     OtherLetter
public readonly ref struct NameStart : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public NameStart(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var letter = new Letter(input);
            index = 1;
            Length = letter.Length;
        }
        catch (ParseException)
        {
            var otherLetter = new OtherLetter(input);
            index = 2;
            Length = otherLetter.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new Letter(input, input, 3)); break;
            case 2: visitor.Visit(new OtherLetter(input, input, 3)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new Letter(input)); break;
            case 2: visitor.Visit(new OtherLetter(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in Letter letter);
        void Visit(in OtherLetter otherLetter);
    }
}

// Number -> [\p{Decimal_Number}]
public readonly ref struct Number : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public Number(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || !IsDecimalNumberChar(input[0]))
            throw new ParseException(new ParseError());
        var i = 1;
        while (i < input.Length && IsDecimalNumberChar(input[i]))
            i++;
        Length = i;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsDecimalNumberChar(Char c) =>
        Char.IsDigit(c);
}

// NameContinue
//     Letter
//     OtherLetter
//     Number
public readonly ref struct NameContinue : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public NameContinue(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var letter = new Letter(input);
            index = 1;
            Length = letter.Length;
        }
        catch (ParseException)
        {
            try
            {
                var otherLetter = new OtherLetter(input);
                index = 2;
                Length = otherLetter.Length;
            }
            catch (ParseException)
            {
                var number = new Number(input);
                index = 3;
                Length = number.Length;
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new Letter(input, input, 5)); break;
            case 2: visitor.Visit(new OtherLetter(input, input, 5)); break;
            case 3: visitor.Visit(new Number(input, input, 5)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new Letter(input)); break;
            case 2: visitor.Visit(new OtherLetter(input)); break;
            case 3: visitor.Visit(new Number(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in Letter letter);
        void Visit(in OtherLetter otherLetter);
        void Visit(in Number number);
    }
}

// Name -> NameStart NameContinue*
public readonly ref struct Name : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 nameStartStart;
    public Int32 Length { get; }

    public Name(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        nameStartStart = pos;
        var nameStart = new NameStart(input[pos..]);
        pos += nameStart.Length;
        while (true)
        {
            try
            {
                var nextNameContinue = new NameContinue(input[pos..]);
                pos += nextNameContinue.Length;
            }
            catch (ParseException)
            {
                break;
            }
        }
        Length = pos;
    }

    public NameStart NameStart => new(input[nameStartStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var nameStart = new NameStart(input[nameStartStart..], input, 6);
        visitor.Visit(nameStart);
        var pos = nameStartStart + nameStart.Length;
        while (pos < Length)
        {
            try
            {
                var nextNameContinue = new NameContinue(input[pos..], input, 6);
                visitor.Visit(nextNameContinue);
                pos += nextNameContinue.Length;
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

// Group -> '(' Alternative ')'
public readonly ref struct Group : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 alternativeStart;
    public Int32 Length { get; }

    public Group(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '(')
            throw new ParseException(new ParseError());
        pos += 1;
        alternativeStart = pos;
        var alternative = new Alternative(input[pos..]);
        pos += alternative.Length;
        if (pos >= input.Length || input[pos] != ')')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Alternative Alternative => new(input[alternativeStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Alternative(input[alternativeStart..], input, 7));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// RuleRef -> Name
public readonly ref struct RuleRef : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 nameStart;
    public Int32 Length { get; }

    public RuleRef(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        nameStart = pos;
        var name = new Name(input[pos..]);
        pos += name.Length;
        Length = pos;
    }

    public Name Name => new(input[nameStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Name(input[nameStart..], input, 8));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// EscapedChar -> '\\' [0\\tnr\"\']
public readonly ref struct EscapedChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public EscapedChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var i = 0;
        if (i >= input.Length || input[i] != '\\')
            throw new ParseException(new ParseError());
        i++;
        if (i >= input.Length || !Is05CTNR2227Char(input[i]))
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

    private static Boolean Is05CTNR2227Char(Char c) =>
        c == '0' || c == '\\' || c == 't' || c == 'n' || c == 'r' || c == '\"' || c == '\'';
}

// Hexadec -> [0-9a-fA-F]
public readonly ref struct Hexadec : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public Hexadec(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || !Is0To9AToFAToFChar(input[0]))
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean Is0To9AToFAToFChar(Char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}

// HexEscape -> '\\' 'x' '{' Hexadec Hexadec? Hexadec? Hexadec? Hexadec? Hexadec? Hexadec? Hexadec? '}'
public readonly ref struct HexEscape : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 leftStart;
    private readonly Int32 rightStart;
    private readonly Int32 hexadec3Start;
    private readonly Int32 hexadec4Start;
    private readonly Int32 hexadec5Start;
    private readonly Int32 hexadec6Start;
    private readonly Int32 hexadec7Start;
    private readonly Int32 hexadec8Start;
    public Int32 Length { get; }

    public HexEscape(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '\\')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != 'x')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != '{')
            throw new ParseException(new ParseError());
        pos += 1;
        leftStart = pos;
        var left = new Hexadec(input[pos..]);
        pos += left.Length;
        rightStart = pos;
        try
        {
            var right = new Hexadec(input[pos..]);
            pos += right.Length;
        }
        catch (ParseException) { }
        hexadec3Start = pos;
        try
        {
            var hexadec3 = new Hexadec(input[pos..]);
            pos += hexadec3.Length;
        }
        catch (ParseException) { }
        hexadec4Start = pos;
        try
        {
            var hexadec4 = new Hexadec(input[pos..]);
            pos += hexadec4.Length;
        }
        catch (ParseException) { }
        hexadec5Start = pos;
        try
        {
            var hexadec5 = new Hexadec(input[pos..]);
            pos += hexadec5.Length;
        }
        catch (ParseException) { }
        hexadec6Start = pos;
        try
        {
            var hexadec6 = new Hexadec(input[pos..]);
            pos += hexadec6.Length;
        }
        catch (ParseException) { }
        hexadec7Start = pos;
        try
        {
            var hexadec7 = new Hexadec(input[pos..]);
            pos += hexadec7.Length;
        }
        catch (ParseException) { }
        hexadec8Start = pos;
        try
        {
            var hexadec8 = new Hexadec(input[pos..]);
            pos += hexadec8.Length;
        }
        catch (ParseException) { }
        if (pos >= input.Length || input[pos] != '}')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Hexadec Left => new(input[leftStart..]);
    public Hexadec Right => new(input[rightStart..]);
    public Hexadec Hexadec3 => new(input[hexadec3Start..]);
    public Hexadec Hexadec4 => new(input[hexadec4Start..]);
    public Hexadec Hexadec5 => new(input[hexadec5Start..]);
    public Hexadec Hexadec6 => new(input[hexadec6Start..]);
    public Hexadec Hexadec7 => new(input[hexadec7Start..]);
    public Hexadec Hexadec8 => new(input[hexadec8Start..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Hexadec(input[leftStart..], input, 11));
        try
        {
            visitor.Visit(new Hexadec(input[rightStart..], input, 11));
        }
        catch (ParseException) { }
        try
        {
            visitor.Visit(new Hexadec(input[hexadec3Start..], input, 11));
        }
        catch (ParseException) { }
        try
        {
            visitor.Visit(new Hexadec(input[hexadec4Start..], input, 11));
        }
        catch (ParseException) { }
        try
        {
            visitor.Visit(new Hexadec(input[hexadec5Start..], input, 11));
        }
        catch (ParseException) { }
        try
        {
            visitor.Visit(new Hexadec(input[hexadec6Start..], input, 11));
        }
        catch (ParseException) { }
        try
        {
            visitor.Visit(new Hexadec(input[hexadec7Start..], input, 11));
        }
        catch (ParseException) { }
        try
        {
            visitor.Visit(new Hexadec(input[hexadec8Start..], input, 11));
        }
        catch (ParseException) { }
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// Unicode4Escape -> '\\' 'u' Hexadec Hexadec Hexadec Hexadec
public readonly ref struct Unicode4Escape : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 leftStart;
    private readonly Int32 rightStart;
    private readonly Int32 hexadec3Start;
    private readonly Int32 hexadec4Start;
    public Int32 Length { get; }

    public Unicode4Escape(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '\\')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != 'u')
            throw new ParseException(new ParseError());
        pos += 1;
        leftStart = pos;
        var left = new Hexadec(input[pos..]);
        pos += left.Length;
        rightStart = pos;
        var right = new Hexadec(input[pos..]);
        pos += right.Length;
        hexadec3Start = pos;
        var hexadec3 = new Hexadec(input[pos..]);
        pos += hexadec3.Length;
        hexadec4Start = pos;
        var hexadec4 = new Hexadec(input[pos..]);
        pos += hexadec4.Length;
        Length = pos;
    }

    public Hexadec Left => new(input[leftStart..]);
    public Hexadec Right => new(input[rightStart..]);
    public Hexadec Hexadec3 => new(input[hexadec3Start..]);
    public Hexadec Hexadec4 => new(input[hexadec4Start..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Hexadec(input[leftStart..], input, 12));
        visitor.Visit(new Hexadec(input[rightStart..], input, 12));
        visitor.Visit(new Hexadec(input[hexadec3Start..], input, 12));
        visitor.Visit(new Hexadec(input[hexadec4Start..], input, 12));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// Unicode8Escape -> '\\' 'U' Hexadec Hexadec Hexadec Hexadec Hexadec Hexadec Hexadec Hexadec
public readonly ref struct Unicode8Escape : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 leftStart;
    private readonly Int32 rightStart;
    private readonly Int32 hexadec3Start;
    private readonly Int32 hexadec4Start;
    private readonly Int32 hexadec5Start;
    private readonly Int32 hexadec6Start;
    private readonly Int32 hexadec7Start;
    private readonly Int32 hexadec8Start;
    public Int32 Length { get; }

    public Unicode8Escape(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '\\')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != 'U')
            throw new ParseException(new ParseError());
        pos += 1;
        leftStart = pos;
        var left = new Hexadec(input[pos..]);
        pos += left.Length;
        rightStart = pos;
        var right = new Hexadec(input[pos..]);
        pos += right.Length;
        hexadec3Start = pos;
        var hexadec3 = new Hexadec(input[pos..]);
        pos += hexadec3.Length;
        hexadec4Start = pos;
        var hexadec4 = new Hexadec(input[pos..]);
        pos += hexadec4.Length;
        hexadec5Start = pos;
        var hexadec5 = new Hexadec(input[pos..]);
        pos += hexadec5.Length;
        hexadec6Start = pos;
        var hexadec6 = new Hexadec(input[pos..]);
        pos += hexadec6.Length;
        hexadec7Start = pos;
        var hexadec7 = new Hexadec(input[pos..]);
        pos += hexadec7.Length;
        hexadec8Start = pos;
        var hexadec8 = new Hexadec(input[pos..]);
        pos += hexadec8.Length;
        Length = pos;
    }

    public Hexadec Left => new(input[leftStart..]);
    public Hexadec Right => new(input[rightStart..]);
    public Hexadec Hexadec3 => new(input[hexadec3Start..]);
    public Hexadec Hexadec4 => new(input[hexadec4Start..]);
    public Hexadec Hexadec5 => new(input[hexadec5Start..]);
    public Hexadec Hexadec6 => new(input[hexadec6Start..]);
    public Hexadec Hexadec7 => new(input[hexadec7Start..]);
    public Hexadec Hexadec8 => new(input[hexadec8Start..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Hexadec(input[leftStart..], input, 13));
        visitor.Visit(new Hexadec(input[rightStart..], input, 13));
        visitor.Visit(new Hexadec(input[hexadec3Start..], input, 13));
        visitor.Visit(new Hexadec(input[hexadec4Start..], input, 13));
        visitor.Visit(new Hexadec(input[hexadec5Start..], input, 13));
        visitor.Visit(new Hexadec(input[hexadec6Start..], input, 13));
        visitor.Visit(new Hexadec(input[hexadec7Start..], input, 13));
        visitor.Visit(new Hexadec(input[hexadec8Start..], input, 13));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// UnicodeEscapeChar
//     HexEscape
//     Unicode4Escape
//     Unicode8Escape
public readonly ref struct UnicodeEscapeChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public UnicodeEscapeChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var hexEscape = new HexEscape(input);
            index = 1;
            Length = hexEscape.Length;
        }
        catch (ParseException)
        {
            try
            {
                var unicode8Escape = new Unicode8Escape(input);
                index = 3;
                Length = unicode8Escape.Length;
            }
            catch (ParseException)
            {
                var unicode4Escape = new Unicode4Escape(input);
                index = 2;
                Length = unicode4Escape.Length;
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new HexEscape(input, input, 14)); break;
            case 2: visitor.Visit(new Unicode4Escape(input, input, 14)); break;
            case 3: visitor.Visit(new Unicode8Escape(input, input, 14)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new HexEscape(input)); break;
            case 2: visitor.Visit(new Unicode4Escape(input)); break;
            case 3: visitor.Visit(new Unicode8Escape(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in HexEscape hexEscape);
        void Visit(in Unicode4Escape unicode4Escape);
        void Visit(in Unicode8Escape unicode8Escape);
    }
}

// PlainStringChar -> [^\\\"]
public readonly ref struct PlainStringChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public PlainStringChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || !IsNot5C22Char(input[0]))
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsNot5C22Char(Char c) =>
        !(c == '\\' || c == '\"');
}

// StringChar
//     EscapedChar
//     UnicodeEscapeChar
//     PlainStringChar
public readonly ref struct StringChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public StringChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var escapedChar = new EscapedChar(input);
            index = 1;
            Length = escapedChar.Length;
        }
        catch (ParseException)
        {
            try
            {
                var unicodeEscapeChar = new UnicodeEscapeChar(input);
                index = 2;
                Length = unicodeEscapeChar.Length;
            }
            catch (ParseException)
            {
                var plainStringChar = new PlainStringChar(input);
                index = 3;
                Length = plainStringChar.Length;
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new EscapedChar(input, input, 16)); break;
            case 2: visitor.Visit(new UnicodeEscapeChar(input, input, 16)); break;
            case 3: visitor.Visit(new PlainStringChar(input, input, 16)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new EscapedChar(input)); break;
            case 2: visitor.Visit(new UnicodeEscapeChar(input)); break;
            case 3: visitor.Visit(new PlainStringChar(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in EscapedChar escapedChar);
        void Visit(in UnicodeEscapeChar unicodeEscapeChar);
        void Visit(in PlainStringChar plainStringChar);
    }
}

// StringLiteral -> '\"' StringChar* '\"'
public readonly ref struct StringLiteral : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public StringLiteral(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '\"')
            throw new ParseException(new ParseError());
        pos += 1;
        while (true)
        {
            try
            {
                var nextStringChar = new StringChar(input[pos..]);
                pos += nextStringChar.Length;
            }
            catch (ParseException)
            {
                break;
            }
        }
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
                var nextStringChar = new StringChar(input[pos..], input, 17);
                visitor.Visit(nextStringChar);
                pos += nextStringChar.Length;
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

// PlainChar -> [^\\\']
public readonly ref struct PlainChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public PlainChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || !IsNot5C27Char(input[0]))
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsNot5C27Char(Char c) =>
        !(c == '\\' || c == '\'');
}

// CharLiteralContent
//     EscapedChar
//     UnicodeEscapeChar
//     PlainChar
public readonly ref struct CharLiteralContent : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public CharLiteralContent(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var escapedChar = new EscapedChar(input);
            index = 1;
            Length = escapedChar.Length;
        }
        catch (ParseException)
        {
            try
            {
                var unicodeEscapeChar = new UnicodeEscapeChar(input);
                index = 2;
                Length = unicodeEscapeChar.Length;
            }
            catch (ParseException)
            {
                var plainChar = new PlainChar(input);
                index = 3;
                Length = plainChar.Length;
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new EscapedChar(input, input, 19)); break;
            case 2: visitor.Visit(new UnicodeEscapeChar(input, input, 19)); break;
            case 3: visitor.Visit(new PlainChar(input, input, 19)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new EscapedChar(input)); break;
            case 2: visitor.Visit(new UnicodeEscapeChar(input)); break;
            case 3: visitor.Visit(new PlainChar(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in EscapedChar escapedChar);
        void Visit(in UnicodeEscapeChar unicodeEscapeChar);
        void Visit(in PlainChar plainChar);
    }
}

// CharLiteral -> '\'' CharLiteralContent '\''
public readonly ref struct CharLiteral : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 charLiteralContentStart;
    public Int32 Length { get; }

    public CharLiteral(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '\'')
            throw new ParseException(new ParseError());
        pos += 1;
        charLiteralContentStart = pos;
        var charLiteralContent = new CharLiteralContent(input[pos..]);
        pos += charLiteralContent.Length;
        if (pos >= input.Length || input[pos] != '\'')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public CharLiteralContent CharLiteralContent => new(input[charLiteralContentStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new CharLiteralContent(input[charLiteralContentStart..], input, 20));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// ClassEscapedChar -> '\\' [^\n]
public readonly ref struct ClassEscapedChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public ClassEscapedChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var i = 0;
        if (i >= input.Length || input[i] != '\\')
            throw new ParseException(new ParseError());
        i++;
        if (i >= input.Length || !IsNot0AChar(input[i]))
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

    private static Boolean IsNot0AChar(Char c) =>
        !(c == '\n');
}

// ClassLetterOrDigit -> [^]\\-]
public readonly ref struct ClassLetterOrDigit : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public ClassLetterOrDigit(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || !IsNot5D5C2DChar(input[0]))
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsNot5D5C2DChar(Char c) =>
        !(c == ']' || c == '\\' || c == '-');
}

// ClassChar
//     ClassEscapedChar
//     ClassLetterOrDigit
public readonly ref struct ClassChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public ClassChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var classEscapedChar = new ClassEscapedChar(input);
            index = 1;
            Length = classEscapedChar.Length;
        }
        catch (ParseException)
        {
            var classLetterOrDigit = new ClassLetterOrDigit(input);
            index = 2;
            Length = classLetterOrDigit.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new ClassEscapedChar(input, input, 23)); break;
            case 2: visitor.Visit(new ClassLetterOrDigit(input, input, 23)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new ClassEscapedChar(input)); break;
            case 2: visitor.Visit(new ClassLetterOrDigit(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in ClassEscapedChar classEscapedChar);
        void Visit(in ClassLetterOrDigit classLetterOrDigit);
    }
}

// CharRange -> ClassChar '-' ClassChar
public readonly ref struct CharRange : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 leftStart;
    private readonly Int32 rightStart;
    public Int32 Length { get; }

    public CharRange(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        leftStart = pos;
        var left = new ClassChar(input[pos..]);
        pos += left.Length;
        if (pos >= input.Length || input[pos] != '-')
            throw new ParseException(new ParseError());
        pos += 1;
        rightStart = pos;
        var right = new ClassChar(input[pos..]);
        pos += right.Length;
        Length = pos;
    }

    public ClassChar Left => new(input[leftStart..]);
    public ClassChar Right => new(input[rightStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new ClassChar(input[leftStart..], input, 24));
        visitor.Visit(new ClassChar(input[rightStart..], input, 24));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// ShorthandClass -> '\\' [wWdDsS]
public readonly ref struct ShorthandClass : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public ShorthandClass(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var i = 0;
        if (i >= input.Length || input[i] != '\\')
            throw new ParseException(new ParseError());
        i++;
        if (i >= input.Length || !IsWordWDigitDSpaceSChar(input[i]))
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

    private static Boolean IsWordWDigitDSpaceSChar(Char c) =>
        (Char.IsLetter(c) || c == '_') || c == 'W' || Char.IsAsciiDigit(c) || c == 'D' || Char.IsWhiteSpace(c) || c == 'S';
}

// LetterCategory
//     "L"
//     "Letter"
//     "Lu"
//     "Uppercase_Letter"
//     "Ll"
//     "Lowercase_Letter"
//     "Lt"
//     "Titlecase_Letter"
//     "Lm"
//     "Modifier_Letter"
//     "Lo"
//     "Other_Letter"
public readonly ref struct LetterCategory : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public LetterCategory(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length >= 16 && input[..16] is "Uppercase_Letter" or "Lowercase_Letter" or "Titlecase_Letter")
        {
            Length = 16;
            return;
        }
        if (input.Length >= 15 && input[..15] is "Modifier_Letter")
        {
            Length = 15;
            return;
        }
        if (input.Length >= 12 && input[..12] is "Other_Letter")
        {
            Length = 12;
            return;
        }
        if (input.Length >= 6 && input[..6] is "Letter")
        {
            Length = 6;
            return;
        }
        if (input.Length >= 2 && input[..2] is "Lu" or "Ll" or "Lt" or "Lm" or "Lo")
        {
            Length = 2;
            return;
        }
        if (input.Length >= 1 && input[..1] is "L")
        {
            Length = 1;
            return;
        }
        throw new ParseException(new ParseError());
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// MarkCategory
//     "M"
//     "Mark"
//     "Mn"
//     "Nonspacing_Mark"
//     "Mc"
//     "Spacing_Mark"
//     "Me"
//     "Enclosing_Mark"
public readonly ref struct MarkCategory : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public MarkCategory(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length >= 15 && input[..15] is "Nonspacing_Mark")
        {
            Length = 15;
            return;
        }
        if (input.Length >= 14 && input[..14] is "Enclosing_Mark")
        {
            Length = 14;
            return;
        }
        if (input.Length >= 12 && input[..12] is "Spacing_Mark")
        {
            Length = 12;
            return;
        }
        if (input.Length >= 4 && input[..4] is "Mark")
        {
            Length = 4;
            return;
        }
        if (input.Length >= 2 && input[..2] is "Mn" or "Mc" or "Me")
        {
            Length = 2;
            return;
        }
        if (input.Length >= 1 && input[..1] is "M")
        {
            Length = 1;
            return;
        }
        throw new ParseException(new ParseError());
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// NumberCategory
//     "N"
//     "Number"
//     "Nd"
//     "Decimal_Number"
//     "Nl"
//     "Letter_Number"
//     "No"
//     "Other_Number"
public readonly ref struct NumberCategory : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public NumberCategory(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length >= 14 && input[..14] is "Decimal_Number")
        {
            Length = 14;
            return;
        }
        if (input.Length >= 13 && input[..13] is "Letter_Number")
        {
            Length = 13;
            return;
        }
        if (input.Length >= 12 && input[..12] is "Other_Number")
        {
            Length = 12;
            return;
        }
        if (input.Length >= 6 && input[..6] is "Number")
        {
            Length = 6;
            return;
        }
        if (input.Length >= 2 && input[..2] is "Nd" or "Nl" or "No")
        {
            Length = 2;
            return;
        }
        if (input.Length >= 1 && input[..1] is "N")
        {
            Length = 1;
            return;
        }
        throw new ParseException(new ParseError());
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// PunctuationCategory
//     "P"
//     "Punctuation"
//     "Pc"
//     "Connector_Punctuation"
//     "Pd"
//     "Dash_Punctuation"
//     "Ps"
//     "Open_Punctuation"
//     "Pe"
//     "Close_Punctuation"
//     "Pi"
//     "Initial_Punctuation"
//     "Pf"
//     "Final_Punctuation"
//     "Po"
//     "Other_Punctuation"
public readonly ref struct PunctuationCategory : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public PunctuationCategory(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length >= 21 && input[..21] is "Connector_Punctuation")
        {
            Length = 21;
            return;
        }
        if (input.Length >= 19 && input[..19] is "Initial_Punctuation")
        {
            Length = 19;
            return;
        }
        if (input.Length >= 17 && input[..17] is "Close_Punctuation" or "Final_Punctuation" or "Other_Punctuation")
        {
            Length = 17;
            return;
        }
        if (input.Length >= 16 && input[..16] is "Dash_Punctuation" or "Open_Punctuation")
        {
            Length = 16;
            return;
        }
        if (input.Length >= 11 && input[..11] is "Punctuation")
        {
            Length = 11;
            return;
        }
        if (input.Length >= 2 && input[..2] is "Pc" or "Pd" or "Ps" or "Pe" or "Pi" or "Pf" or "Po")
        {
            Length = 2;
            return;
        }
        if (input.Length >= 1 && input[..1] is "P")
        {
            Length = 1;
            return;
        }
        throw new ParseException(new ParseError());
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// SymbolCategory
//     "S"
//     "Symbol"
//     "Sm"
//     "Math_Symbol"
//     "Sc"
//     "Currency_Symbol"
//     "Sk"
//     "Modifier_Symbol"
//     "So"
//     "Other_Symbol"
public readonly ref struct SymbolCategory : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public SymbolCategory(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length >= 15 && input[..15] is "Currency_Symbol" or "Modifier_Symbol")
        {
            Length = 15;
            return;
        }
        if (input.Length >= 12 && input[..12] is "Other_Symbol")
        {
            Length = 12;
            return;
        }
        if (input.Length >= 11 && input[..11] is "Math_Symbol")
        {
            Length = 11;
            return;
        }
        if (input.Length >= 6 && input[..6] is "Symbol")
        {
            Length = 6;
            return;
        }
        if (input.Length >= 2 && input[..2] is "Sm" or "Sc" or "Sk" or "So")
        {
            Length = 2;
            return;
        }
        if (input.Length >= 1 && input[..1] is "S")
        {
            Length = 1;
            return;
        }
        throw new ParseException(new ParseError());
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// SeparatorCategory
//     "Z"
//     "Separator"
//     "Zs"
//     "Space_Separator"
//     "Zl"
//     "Line_Separator"
//     "Zp"
//     "Paragraph_Separator"
public readonly ref struct SeparatorCategory : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public SeparatorCategory(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length >= 19 && input[..19] is "Paragraph_Separator")
        {
            Length = 19;
            return;
        }
        if (input.Length >= 15 && input[..15] is "Space_Separator")
        {
            Length = 15;
            return;
        }
        if (input.Length >= 14 && input[..14] is "Line_Separator")
        {
            Length = 14;
            return;
        }
        if (input.Length >= 9 && input[..9] is "Separator")
        {
            Length = 9;
            return;
        }
        if (input.Length >= 2 && input[..2] is "Zs" or "Zl" or "Zp")
        {
            Length = 2;
            return;
        }
        if (input.Length >= 1 && input[..1] is "Z")
        {
            Length = 1;
            return;
        }
        throw new ParseException(new ParseError());
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// OtherCategory
//     "C"
//     "Other"
//     "Cc"
//     "Control"
//     "Cf"
//     "Format"
//     "Cs"
//     "Surrogate"
//     "Co"
//     "Private_Use"
//     "Cn"
//     "Unassigned"
public readonly ref struct OtherCategory : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public OtherCategory(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length >= 11 && input[..11] is "Private_Use")
        {
            Length = 11;
            return;
        }
        if (input.Length >= 10 && input[..10] is "Unassigned")
        {
            Length = 10;
            return;
        }
        if (input.Length >= 9 && input[..9] is "Surrogate")
        {
            Length = 9;
            return;
        }
        if (input.Length >= 7 && input[..7] is "Control")
        {
            Length = 7;
            return;
        }
        if (input.Length >= 6 && input[..6] is "Format")
        {
            Length = 6;
            return;
        }
        if (input.Length >= 5 && input[..5] is "Other")
        {
            Length = 5;
            return;
        }
        if (input.Length >= 2 && input[..2] is "Cc" or "Cf" or "Cs" or "Co" or "Cn")
        {
            Length = 2;
            return;
        }
        if (input.Length >= 1 && input[..1] is "C")
        {
            Length = 1;
            return;
        }
        throw new ParseException(new ParseError());
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// CategoryName
//     LetterCategory
//     MarkCategory
//     NumberCategory
//     PunctuationCategory
//     SymbolCategory
//     SeparatorCategory
//     OtherCategory
public readonly ref struct CategoryName : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public CategoryName(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var letterCategory = new LetterCategory(input);
            index = 1;
            Length = letterCategory.Length;
        }
        catch (ParseException)
        {
            try
            {
                var markCategory = new MarkCategory(input);
                index = 2;
                Length = markCategory.Length;
            }
            catch (ParseException)
            {
                try
                {
                    var numberCategory = new NumberCategory(input);
                    index = 3;
                    Length = numberCategory.Length;
                }
                catch (ParseException)
                {
                    try
                    {
                        var punctuationCategory = new PunctuationCategory(input);
                        index = 4;
                        Length = punctuationCategory.Length;
                    }
                    catch (ParseException)
                    {
                        try
                        {
                            var symbolCategory = new SymbolCategory(input);
                            index = 5;
                            Length = symbolCategory.Length;
                        }
                        catch (ParseException)
                        {
                            try
                            {
                                var separatorCategory = new SeparatorCategory(input);
                                index = 6;
                                Length = separatorCategory.Length;
                            }
                            catch (ParseException)
                            {
                                var otherCategory = new OtherCategory(input);
                                index = 7;
                                Length = otherCategory.Length;
                            }
                        }
                    }
                }
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new LetterCategory(input, input, 33)); break;
            case 2: visitor.Visit(new MarkCategory(input, input, 33)); break;
            case 3: visitor.Visit(new NumberCategory(input, input, 33)); break;
            case 4: visitor.Visit(new PunctuationCategory(input, input, 33)); break;
            case 5: visitor.Visit(new SymbolCategory(input, input, 33)); break;
            case 6: visitor.Visit(new SeparatorCategory(input, input, 33)); break;
            case 7: visitor.Visit(new OtherCategory(input, input, 33)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new LetterCategory(input)); break;
            case 2: visitor.Visit(new MarkCategory(input)); break;
            case 3: visitor.Visit(new NumberCategory(input)); break;
            case 4: visitor.Visit(new PunctuationCategory(input)); break;
            case 5: visitor.Visit(new SymbolCategory(input)); break;
            case 6: visitor.Visit(new SeparatorCategory(input)); break;
            case 7: visitor.Visit(new OtherCategory(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in LetterCategory letterCategory);
        void Visit(in MarkCategory markCategory);
        void Visit(in NumberCategory numberCategory);
        void Visit(in PunctuationCategory punctuationCategory);
        void Visit(in SymbolCategory symbolCategory);
        void Visit(in SeparatorCategory separatorCategory);
        void Visit(in OtherCategory otherCategory);
    }
}

// GeneralCategoryProperty -> "General_Category=" CategoryName
public readonly ref struct GeneralCategoryProperty : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 categoryNameStart;
    public Int32 Length { get; }

    public GeneralCategoryProperty(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (!input[pos..].StartsWith("General_Category="))
            throw new ParseException(new ParseError());
        pos += 17;
        categoryNameStart = pos;
        var categoryName = new CategoryName(input[pos..]);
        pos += categoryName.Length;
        Length = pos;
    }

    public CategoryName CategoryName => new(input[categoryNameStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new CategoryName(input[categoryNameStart..], input, 34));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// BinaryProperty
//     "Alpha"
//     "Alphabetic"
//     "Upper"
//     "Uppercase"
//     "Lower"
//     "Lowercase"
//     "White_Space"
public readonly ref struct BinaryProperty : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public BinaryProperty(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length >= 11 && input[..11] is "White_Space")
        {
            Length = 11;
            return;
        }
        if (input.Length >= 10 && input[..10] is "Alphabetic")
        {
            Length = 10;
            return;
        }
        if (input.Length >= 9 && input[..9] is "Uppercase" or "Lowercase")
        {
            Length = 9;
            return;
        }
        if (input.Length >= 5 && input[..5] is "Alpha" or "Upper" or "Lower")
        {
            Length = 5;
            return;
        }
        throw new ParseException(new ParseError());
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// PropertyName
//     GeneralCategoryProperty
//     CategoryName
//     BinaryProperty
public readonly ref struct PropertyName : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public PropertyName(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.StartsWith("General_Category="))
        {
            var generalCategoryProperty = new GeneralCategoryProperty(input);
            index = 1;
            Length = generalCategoryProperty.Length;
        }
        else
        {
            try
            {
                var categoryName = new CategoryName(input);
                index = 2;
                Length = categoryName.Length;
            }
            catch (ParseException)
            {
                var binaryProperty = new BinaryProperty(input);
                index = 3;
                Length = binaryProperty.Length;
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new GeneralCategoryProperty(input, input, 36)); break;
            case 2: visitor.Visit(new CategoryName(input, input, 36)); break;
            case 3: visitor.Visit(new BinaryProperty(input, input, 36)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new GeneralCategoryProperty(input)); break;
            case 2: visitor.Visit(new CategoryName(input)); break;
            case 3: visitor.Visit(new BinaryProperty(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in GeneralCategoryProperty generalCategoryProperty);
        void Visit(in CategoryName categoryName);
        void Visit(in BinaryProperty binaryProperty);
    }
}

// UnicodePropertyClass -> '\\' 'p' '{' PropertyName '}'
public readonly ref struct UnicodePropertyClass : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 propertyNameStart;
    public Int32 Length { get; }

    public UnicodePropertyClass(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '\\')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != 'p')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != '{')
            throw new ParseException(new ParseError());
        pos += 1;
        propertyNameStart = pos;
        var propertyName = new PropertyName(input[pos..]);
        pos += propertyName.Length;
        if (pos >= input.Length || input[pos] != '}')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public PropertyName PropertyName => new(input[propertyNameStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new PropertyName(input[propertyNameStart..], input, 37));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// SingleClassChar -> ClassChar
public readonly ref struct SingleClassChar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 classCharStart;
    public Int32 Length { get; }

    public SingleClassChar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        classCharStart = pos;
        var classChar = new ClassChar(input[pos..]);
        pos += classChar.Length;
        Length = pos;
    }

    public ClassChar ClassChar => new(input[classCharStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new ClassChar(input[classCharStart..], input, 38));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// ClassRange
//     CharRange
//     ShorthandClass
//     UnicodePropertyClass
//     SingleClassChar
public readonly ref struct ClassRange : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public ClassRange(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var unicodePropertyClass = new UnicodePropertyClass(input);
            index = 3;
            Length = unicodePropertyClass.Length;
        }
        catch (ParseException)
        {
            try
            {
                var charRange = new CharRange(input);
                index = 1;
                Length = charRange.Length;
            }
            catch (ParseException)
            {
                try
                {
                    var shorthandClass = new ShorthandClass(input);
                    index = 2;
                    Length = shorthandClass.Length;
                }
                catch (ParseException)
                {
                    var singleClassChar = new SingleClassChar(input);
                    index = 4;
                    Length = singleClassChar.Length;
                }
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new CharRange(input, input, 39)); break;
            case 2: visitor.Visit(new ShorthandClass(input, input, 39)); break;
            case 3: visitor.Visit(new UnicodePropertyClass(input, input, 39)); break;
            case 4: visitor.Visit(new SingleClassChar(input, input, 39)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new CharRange(input)); break;
            case 2: visitor.Visit(new ShorthandClass(input)); break;
            case 3: visitor.Visit(new UnicodePropertyClass(input)); break;
            case 4: visitor.Visit(new SingleClassChar(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in CharRange charRange);
        void Visit(in ShorthandClass shorthandClass);
        void Visit(in UnicodePropertyClass unicodePropertyClass);
        void Visit(in SingleClassChar singleClassChar);
    }
}

// BracketedClass -> '[' '^' ClassRange+ ']'
public readonly ref struct BracketedClass : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public BracketedClass(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '[')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos < input.Length && input[pos] == '^')
            pos += 1;
        var firstClassRange = new ClassRange(input[pos..]);
        pos += firstClassRange.Length;
        while (true)
        {
            try
            {
                var nextClassRange = new ClassRange(input[pos..]);
                pos += nextClassRange.Length;
            }
            catch (ParseException)
            {
                break;
            }
        }
        if (pos >= input.Length || input[pos] != ']')
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
                var nextClassRange = new ClassRange(input[pos..], input, 40);
                visitor.Visit(nextClassRange);
                pos += nextClassRange.Length;
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

// Class
//     BracketedClass
//     ClassRange
public readonly ref struct Class : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public Class(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var bracketedClass = new BracketedClass(input);
            index = 1;
            Length = bracketedClass.Length;
        }
        catch (ParseException)
        {
            var classRange = new ClassRange(input);
            index = 2;
            Length = classRange.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new BracketedClass(input, input, 41)); break;
            case 2: visitor.Visit(new ClassRange(input, input, 41)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new BracketedClass(input)); break;
            case 2: visitor.Visit(new ClassRange(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in BracketedClass bracketedClass);
        void Visit(in ClassRange classRange);
    }
}

// Atom
//     Group
//     RuleRef
//     StringLiteral
//     CharLiteral
//     Class
public readonly ref struct Atom : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public Atom(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var group = new Group(input);
            index = 1;
            Length = group.Length;
        }
        catch (ParseException)
        {
            try
            {
                var stringLiteral = new StringLiteral(input);
                index = 3;
                Length = stringLiteral.Length;
            }
            catch (ParseException)
            {
                try
                {
                    var charLiteral = new CharLiteral(input);
                    index = 4;
                    Length = charLiteral.Length;
                }
                catch (ParseException)
                {
                    try
                    {
                        var ruleRef = new RuleRef(input);
                        index = 2;
                        Length = ruleRef.Length;
                    }
                    catch (ParseException)
                    {
                        var @class = new Class(input);
                        index = 5;
                        Length = @class.Length;
                    }
                }
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new Group(input, input, 42)); break;
            case 2: visitor.Visit(new RuleRef(input, input, 42)); break;
            case 3: visitor.Visit(new StringLiteral(input, input, 42)); break;
            case 4: visitor.Visit(new CharLiteral(input, input, 42)); break;
            case 5: visitor.Visit(new Class(input, input, 42)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new Group(input)); break;
            case 2: visitor.Visit(new RuleRef(input)); break;
            case 3: visitor.Visit(new StringLiteral(input)); break;
            case 4: visitor.Visit(new CharLiteral(input)); break;
            case 5: visitor.Visit(new Class(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in Group group);
        void Visit(in RuleRef ruleRef);
        void Visit(in StringLiteral stringLiteral);
        void Visit(in CharLiteral charLiteral);
        void Visit(in Class @class);
    }
}

// ZeroOrOne -> '?'
public readonly ref struct ZeroOrOne : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public ZeroOrOne(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || input[0] != '?')
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// ZeroOrMore -> '*'
public readonly ref struct ZeroOrMore : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public ZeroOrMore(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || input[0] != '*')
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// OneOrMore -> '+'
public readonly ref struct OneOrMore : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public OneOrMore(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length == 0 || input[0] != '+')
            throw new ParseException(new ParseError());
        Length = 1;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// Exactly -> '{' Number '}'
public readonly ref struct Exactly : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 numberStart;
    public Int32 Length { get; }

    public Exactly(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '{')
            throw new ParseException(new ParseError());
        pos += 1;
        numberStart = pos;
        var number = new Number(input[pos..]);
        pos += number.Length;
        if (pos >= input.Length || input[pos] != '}')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Number Number => new(input[numberStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Number(input[numberStart..], input, 46));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// AtLeast -> '{' Number ',' '}'
public readonly ref struct AtLeast : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 numberStart;
    public Int32 Length { get; }

    public AtLeast(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '{')
            throw new ParseException(new ParseError());
        pos += 1;
        numberStart = pos;
        var number = new Number(input[pos..]);
        pos += number.Length;
        if (pos >= input.Length || input[pos] != ',')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != '}')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Number Number => new(input[numberStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Number(input[numberStart..], input, 47));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// Between -> '{' Number ',' Number '}'
public readonly ref struct Between : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 leftStart;
    private readonly Int32 rightStart;
    public Int32 Length { get; }

    public Between(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '{')
            throw new ParseException(new ParseError());
        pos += 1;
        leftStart = pos;
        var left = new Number(input[pos..]);
        pos += left.Length;
        if (pos >= input.Length || input[pos] != ',')
            throw new ParseException(new ParseError());
        pos += 1;
        rightStart = pos;
        var right = new Number(input[pos..]);
        pos += right.Length;
        if (pos >= input.Length || input[pos] != '}')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Number Left => new(input[leftStart..]);
    public Number Right => new(input[rightStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Number(input[leftStart..], input, 48));
        visitor.Visit(new Number(input[rightStart..], input, 48));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// Quantifier
//     ZeroOrOne
//     ZeroOrMore
//     OneOrMore
//     Exactly
//     AtLeast
//     Between
public readonly ref struct Quantifier : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public Quantifier(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var between = new Between(input);
            index = 6;
            Length = between.Length;
        }
        catch (ParseException)
        {
            try
            {
                var atLeast = new AtLeast(input);
                index = 5;
                Length = atLeast.Length;
            }
            catch (ParseException)
            {
                try
                {
                    var exactly = new Exactly(input);
                    index = 4;
                    Length = exactly.Length;
                }
                catch (ParseException)
                {
                    try
                    {
                        var zeroOrOne = new ZeroOrOne(input);
                        index = 1;
                        Length = zeroOrOne.Length;
                    }
                    catch (ParseException)
                    {
                        try
                        {
                            var zeroOrMore = new ZeroOrMore(input);
                            index = 2;
                            Length = zeroOrMore.Length;
                        }
                        catch (ParseException)
                        {
                            var oneOrMore = new OneOrMore(input);
                            index = 3;
                            Length = oneOrMore.Length;
                        }
                    }
                }
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new ZeroOrOne(input, input, 49)); break;
            case 2: visitor.Visit(new ZeroOrMore(input, input, 49)); break;
            case 3: visitor.Visit(new OneOrMore(input, input, 49)); break;
            case 4: visitor.Visit(new Exactly(input, input, 49)); break;
            case 5: visitor.Visit(new AtLeast(input, input, 49)); break;
            case 6: visitor.Visit(new Between(input, input, 49)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new ZeroOrOne(input)); break;
            case 2: visitor.Visit(new ZeroOrMore(input)); break;
            case 3: visitor.Visit(new OneOrMore(input)); break;
            case 4: visitor.Visit(new Exactly(input)); break;
            case 5: visitor.Visit(new AtLeast(input)); break;
            case 6: visitor.Visit(new Between(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in ZeroOrOne zeroOrOne);
        void Visit(in ZeroOrMore zeroOrMore);
        void Visit(in OneOrMore oneOrMore);
        void Visit(in Exactly exactly);
        void Visit(in AtLeast atLeast);
        void Visit(in Between between);
    }
}

// Item -> Atom Quantifier?
public readonly ref struct Item : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 atomStart;
    private readonly Int32 quantifierStart;
    public Int32 Length { get; }

    public Item(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        atomStart = pos;
        var atom = new Atom(input[pos..]);
        pos += atom.Length;
        quantifierStart = pos;
        try
        {
            var quantifier = new Quantifier(input[pos..]);
            pos += quantifier.Length;
        }
        catch (ParseException) { }
        Length = pos;
    }

    public Atom Atom => new(input[atomStart..]);
    public Quantifier Quantifier => new(input[quantifierStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Atom(input[atomStart..], input, 50));
        try
        {
            visitor.Visit(new Quantifier(input[quantifierStart..], input, 50));
        }
        catch (ParseException) { }
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// Alternative -> Item (' ' Item)*
public readonly ref struct Alternative : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 itemStart;
    public Int32 Length { get; }

    public Alternative(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        itemStart = pos;
        var item = new Item(input[pos..]);
        pos += item.Length;
        while (pos < input.Length && input[pos] == ' ')
        {
            pos += 1;
            var next = new Item(input[pos..]);
            pos += next.Length;
        }
        Length = pos;
    }

    public Item Item => new(input[itemStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var item = new Item(input[itemStart..], input, 51);
        visitor.Visit(item);
        var pos = itemStart + item.Length;
        while (pos < Length)
        {
            try
            {
                if (pos >= input.Length || input[pos] != ' ') break;
                pos += 1;
                var nextItem = new Item(input[pos..], input, 51);
                visitor.Visit(nextItem);
                pos += nextItem.Length;
            }
            catch (ParseException) { break; }
        }
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// InlineAlternatives -> ' ' '-' '>' ' ' Alternative
public readonly ref struct InlineAlternatives : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 alternativeStart;
    public Int32 Length { get; }

    public InlineAlternatives(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != ' ')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != '-')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != '>')
            throw new ParseException(new ParseError());
        pos += 1;
        if (pos >= input.Length || input[pos] != ' ')
            throw new ParseException(new ParseError());
        pos += 1;
        alternativeStart = pos;
        var alternative = new Alternative(input[pos..]);
        pos += alternative.Length;
        Length = pos;
    }

    public Alternative Alternative => new(input[alternativeStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Alternative(input[alternativeStart..], input, 52));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// Indentation
//     '\t'
//     "    "
public readonly ref struct Indentation : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public Indentation(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length > 0 && input[0] == '\t')
        {
            if (input.Length == 0 || input[0] != '\t')
                throw new ParseException(new ParseError());
            Length = 1;
        }
        else
        {
            if (!input.StartsWith("    "))
                throw new ParseException(new ParseError());
            Length = 4;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// IndentedAlternative -> '\n' Indentation Alternative
public readonly ref struct IndentedAlternative : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 indentationStart;
    private readonly Int32 alternativeStart;
    public Int32 Length { get; }

    public IndentedAlternative(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        if (pos >= input.Length || input[pos] != '\n')
            throw new ParseException(new ParseError());
        pos += 1;
        indentationStart = pos;
        var indentation = new Indentation(input[pos..]);
        pos += indentation.Length;
        alternativeStart = pos;
        var alternative = new Alternative(input[pos..]);
        pos += alternative.Length;
        Length = pos;
    }

    public Indentation Indentation => new(input[indentationStart..]);
    public Alternative Alternative => new(input[alternativeStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Indentation(input[indentationStart..], input, 54));
        visitor.Visit(new Alternative(input[alternativeStart..], input, 54));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// IndentedAlternatives -> IndentedAlternative+
public readonly ref struct IndentedAlternatives : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public IndentedAlternatives(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        var firstIndentedAlternative = new IndentedAlternative(input[pos..]);
        pos += firstIndentedAlternative.Length;
        while (true)
        {
            try
            {
                var nextIndentedAlternative = new IndentedAlternative(input[pos..]);
                pos += nextIndentedAlternative.Length;
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
                var nextIndentedAlternative = new IndentedAlternative(input[pos..], input, 55);
                visitor.Visit(nextIndentedAlternative);
                pos += nextIndentedAlternative.Length;
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

// Alternatives
//     InlineAlternatives
//     IndentedAlternatives
public readonly ref struct Alternatives : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Byte index;
    public Int32 Length { get; }

    public Alternatives(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        try
        {
            var inlineAlternatives = new InlineAlternatives(input);
            index = 1;
            Length = inlineAlternatives.Length;
        }
        catch (ParseException)
        {
            var indentedAlternatives = new IndentedAlternatives(input);
            index = 2;
            Length = indentedAlternatives.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new InlineAlternatives(input, input, 56)); break;
            case 2: visitor.Visit(new IndentedAlternatives(input, input, 56)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new InlineAlternatives(input)); break;
            case 2: visitor.Visit(new IndentedAlternatives(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in InlineAlternatives inlineAlternatives);
        void Visit(in IndentedAlternatives indentedAlternatives);
    }
}

// Rule -> Name Alternatives
public readonly ref struct Rule : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 nameStart;
    private readonly Int32 alternativesStart;
    public Int32 Length { get; }

    public Rule(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        nameStart = pos;
        var name = new Name(input[pos..]);
        pos += name.Length;
        alternativesStart = pos;
        var alternatives = new Alternatives(input[pos..]);
        pos += alternatives.Length;
        Length = pos;
    }

    public Name Name => new(input[nameStart..]);
    public Alternatives Alternatives => new(input[alternativesStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Name(input[nameStart..], input, 57));
        visitor.Visit(new Alternatives(input[alternativesStart..], input, 57));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

}

// Grammar -> Rule ('\n' '\n' Rule)*
public readonly ref struct Grammar : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    private readonly Int32 ruleStart;
    public Int32 Length { get; }

    public Grammar(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var pos = 0;
        ruleStart = pos;
        var rule = new Rule(input[pos..]);
        pos += rule.Length;
        while (true)
        {
            var savedPos = pos;
            try
            {
                if (pos >= input.Length || input[pos] != '\n')
                    throw new ParseException(new ParseError());
                pos += 1;
                if (pos >= input.Length || input[pos] != '\n')
                    throw new ParseException(new ParseError());
                pos += 1;
                while (pos < input.Length && input[pos] == '\n')
                    pos += 1;
                var nextRule = new Rule(input[pos..]);
                pos += nextRule.Length;
            }
            catch (ParseException)
            {
                pos = savedPos;
                break;
            }
        }
        Length = pos;
    }

    public Rule Rule => new(input[ruleStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var rule = new Rule(input[ruleStart..], input, 58);
        visitor.Visit(rule);
        var pos = ruleStart + rule.Length;
        while (pos < Length)
        {
            try
            {
                if (pos >= input.Length || input[pos] != '\n') break;
                pos += 1;
                if (pos >= input.Length || input[pos] != '\n') break;
                pos += 1;
                while (pos < input.Length && input[pos] == '\n')
                    pos += 1;
                var nextRule = new Rule(input[pos..], input, 58);
                visitor.Visit(nextRule);
                pos += nextRule.Length;
            }
            catch (ParseException) { break; }
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
            case 1: visitor.Visit(new Letter(input)); break;
            case 2: visitor.Visit(new OtherLetter(input)); break;
            case 3: visitor.Visit(new NameStart(input)); break;
            case 4: visitor.Visit(new Number(input)); break;
            case 5: visitor.Visit(new NameContinue(input)); break;
            case 6: visitor.Visit(new Name(input)); break;
            case 7: visitor.Visit(new Group(input)); break;
            case 8: visitor.Visit(new RuleRef(input)); break;
            case 9: visitor.Visit(new EscapedChar(input)); break;
            case 10: visitor.Visit(new Hexadec(input)); break;
            case 11: visitor.Visit(new HexEscape(input)); break;
            case 12: visitor.Visit(new Unicode4Escape(input)); break;
            case 13: visitor.Visit(new Unicode8Escape(input)); break;
            case 14: visitor.Visit(new UnicodeEscapeChar(input)); break;
            case 15: visitor.Visit(new PlainStringChar(input)); break;
            case 16: visitor.Visit(new StringChar(input)); break;
            case 17: visitor.Visit(new StringLiteral(input)); break;
            case 18: visitor.Visit(new PlainChar(input)); break;
            case 19: visitor.Visit(new CharLiteralContent(input)); break;
            case 20: visitor.Visit(new CharLiteral(input)); break;
            case 21: visitor.Visit(new ClassEscapedChar(input)); break;
            case 22: visitor.Visit(new ClassLetterOrDigit(input)); break;
            case 23: visitor.Visit(new ClassChar(input)); break;
            case 24: visitor.Visit(new CharRange(input)); break;
            case 25: visitor.Visit(new ShorthandClass(input)); break;
            case 26: visitor.Visit(new LetterCategory(input)); break;
            case 27: visitor.Visit(new MarkCategory(input)); break;
            case 28: visitor.Visit(new NumberCategory(input)); break;
            case 29: visitor.Visit(new PunctuationCategory(input)); break;
            case 30: visitor.Visit(new SymbolCategory(input)); break;
            case 31: visitor.Visit(new SeparatorCategory(input)); break;
            case 32: visitor.Visit(new OtherCategory(input)); break;
            case 33: visitor.Visit(new CategoryName(input)); break;
            case 34: visitor.Visit(new GeneralCategoryProperty(input)); break;
            case 35: visitor.Visit(new BinaryProperty(input)); break;
            case 36: visitor.Visit(new PropertyName(input)); break;
            case 37: visitor.Visit(new UnicodePropertyClass(input)); break;
            case 38: visitor.Visit(new SingleClassChar(input)); break;
            case 39: visitor.Visit(new ClassRange(input)); break;
            case 40: visitor.Visit(new BracketedClass(input)); break;
            case 41: visitor.Visit(new Class(input)); break;
            case 42: visitor.Visit(new Atom(input)); break;
            case 43: visitor.Visit(new ZeroOrOne(input)); break;
            case 44: visitor.Visit(new ZeroOrMore(input)); break;
            case 45: visitor.Visit(new OneOrMore(input)); break;
            case 46: visitor.Visit(new Exactly(input)); break;
            case 47: visitor.Visit(new AtLeast(input)); break;
            case 48: visitor.Visit(new Between(input)); break;
            case 49: visitor.Visit(new Quantifier(input)); break;
            case 50: visitor.Visit(new Item(input)); break;
            case 51: visitor.Visit(new Alternative(input)); break;
            case 52: visitor.Visit(new InlineAlternatives(input)); break;
            case 53: visitor.Visit(new Indentation(input)); break;
            case 54: visitor.Visit(new IndentedAlternative(input)); break;
            case 55: visitor.Visit(new IndentedAlternatives(input)); break;
            case 56: visitor.Visit(new Alternatives(input)); break;
            case 57: visitor.Visit(new Rule(input)); break;
            case 58: visitor.Visit(new Grammar(input)); break;
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

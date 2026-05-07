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
            { Length = -1; return; }
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
            { Length = -1; return; }
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
        var letter = new Letter(input);
        index = 1;
        Length = letter.Length;
        if (Length >= 0) return;
        var otherLetter = new OtherLetter(input);
        index = 2;
        Length = otherLetter.Length;
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
            { Length = -1; return; }
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
        var letter = new Letter(input);
        index = 1;
        Length = letter.Length;
        if (Length >= 0) return;
        var otherLetter = new OtherLetter(input);
        index = 2;
        Length = otherLetter.Length;
        if (Length >= 0) return;
        var number = new Number(input);
        index = 3;
        Length = number.Length;
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
        if (nameStart.Length < 0) { Length = -1; return; }
        pos += nameStart.Length;
        while (true)
        {
            var nextNameContinue = new NameContinue(input[pos..]);
            if (nextNameContinue.Length < 0) break;
            pos += nextNameContinue.Length;
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
            var nextNameContinue = new NameContinue(input[pos..], input, 6);
            if (nextNameContinue.Length < 0) break;
            visitor.Visit(nextNameContinue);
            pos += nextNameContinue.Length;
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
            { Length = -1; return; }
        pos += 1;
        alternativeStart = pos;
        var alternative = new Alternative(input[pos..]);
        if (alternative.Length < 0) { Length = -1; return; }
        pos += alternative.Length;
        if (pos >= input.Length || input[pos] != ')')
            { Length = -1; return; }
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
        if (name.Length < 0) { Length = -1; return; }
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
            { Length = -1; return; }
        i++;
        if (i >= input.Length || !Is05CTNR2227Char(input[i]))
            { Length = -1; return; }
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
            { Length = -1; return; }
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
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != 'x')
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != '{')
            { Length = -1; return; }
        pos += 1;
        leftStart = pos;
        var left = new Hexadec(input[pos..]);
        if (left.Length < 0) { Length = -1; return; }
        pos += left.Length;
        rightStart = pos;
        var right = new Hexadec(input[pos..]);
        if (right.Length >= 0) pos += right.Length;
        hexadec3Start = pos;
        var hexadec3 = new Hexadec(input[pos..]);
        if (hexadec3.Length >= 0) pos += hexadec3.Length;
        hexadec4Start = pos;
        var hexadec4 = new Hexadec(input[pos..]);
        if (hexadec4.Length >= 0) pos += hexadec4.Length;
        hexadec5Start = pos;
        var hexadec5 = new Hexadec(input[pos..]);
        if (hexadec5.Length >= 0) pos += hexadec5.Length;
        hexadec6Start = pos;
        var hexadec6 = new Hexadec(input[pos..]);
        if (hexadec6.Length >= 0) pos += hexadec6.Length;
        hexadec7Start = pos;
        var hexadec7 = new Hexadec(input[pos..]);
        if (hexadec7.Length >= 0) pos += hexadec7.Length;
        hexadec8Start = pos;
        var hexadec8 = new Hexadec(input[pos..]);
        if (hexadec8.Length >= 0) pos += hexadec8.Length;
        if (pos >= input.Length || input[pos] != '}')
            { Length = -1; return; }
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
        var rightOpt = new Hexadec(input[rightStart..], input, 11);
        if (rightOpt.Length >= 0) visitor.Visit(rightOpt);
        var hexadec3Opt = new Hexadec(input[hexadec3Start..], input, 11);
        if (hexadec3Opt.Length >= 0) visitor.Visit(hexadec3Opt);
        var hexadec4Opt = new Hexadec(input[hexadec4Start..], input, 11);
        if (hexadec4Opt.Length >= 0) visitor.Visit(hexadec4Opt);
        var hexadec5Opt = new Hexadec(input[hexadec5Start..], input, 11);
        if (hexadec5Opt.Length >= 0) visitor.Visit(hexadec5Opt);
        var hexadec6Opt = new Hexadec(input[hexadec6Start..], input, 11);
        if (hexadec6Opt.Length >= 0) visitor.Visit(hexadec6Opt);
        var hexadec7Opt = new Hexadec(input[hexadec7Start..], input, 11);
        if (hexadec7Opt.Length >= 0) visitor.Visit(hexadec7Opt);
        var hexadec8Opt = new Hexadec(input[hexadec8Start..], input, 11);
        if (hexadec8Opt.Length >= 0) visitor.Visit(hexadec8Opt);
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
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != 'u')
            { Length = -1; return; }
        pos += 1;
        leftStart = pos;
        var left = new Hexadec(input[pos..]);
        if (left.Length < 0) { Length = -1; return; }
        pos += left.Length;
        rightStart = pos;
        var right = new Hexadec(input[pos..]);
        if (right.Length < 0) { Length = -1; return; }
        pos += right.Length;
        hexadec3Start = pos;
        var hexadec3 = new Hexadec(input[pos..]);
        if (hexadec3.Length < 0) { Length = -1; return; }
        pos += hexadec3.Length;
        hexadec4Start = pos;
        var hexadec4 = new Hexadec(input[pos..]);
        if (hexadec4.Length < 0) { Length = -1; return; }
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
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != 'U')
            { Length = -1; return; }
        pos += 1;
        leftStart = pos;
        var left = new Hexadec(input[pos..]);
        if (left.Length < 0) { Length = -1; return; }
        pos += left.Length;
        rightStart = pos;
        var right = new Hexadec(input[pos..]);
        if (right.Length < 0) { Length = -1; return; }
        pos += right.Length;
        hexadec3Start = pos;
        var hexadec3 = new Hexadec(input[pos..]);
        if (hexadec3.Length < 0) { Length = -1; return; }
        pos += hexadec3.Length;
        hexadec4Start = pos;
        var hexadec4 = new Hexadec(input[pos..]);
        if (hexadec4.Length < 0) { Length = -1; return; }
        pos += hexadec4.Length;
        hexadec5Start = pos;
        var hexadec5 = new Hexadec(input[pos..]);
        if (hexadec5.Length < 0) { Length = -1; return; }
        pos += hexadec5.Length;
        hexadec6Start = pos;
        var hexadec6 = new Hexadec(input[pos..]);
        if (hexadec6.Length < 0) { Length = -1; return; }
        pos += hexadec6.Length;
        hexadec7Start = pos;
        var hexadec7 = new Hexadec(input[pos..]);
        if (hexadec7.Length < 0) { Length = -1; return; }
        pos += hexadec7.Length;
        hexadec8Start = pos;
        var hexadec8 = new Hexadec(input[pos..]);
        if (hexadec8.Length < 0) { Length = -1; return; }
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
        var hexEscape = new HexEscape(input);
        index = 1;
        Length = hexEscape.Length;
        if (Length >= 0) return;
        var unicode8Escape = new Unicode8Escape(input);
        index = 3;
        Length = unicode8Escape.Length;
        if (Length >= 0) return;
        var unicode4Escape = new Unicode4Escape(input);
        index = 2;
        Length = unicode4Escape.Length;
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
            { Length = -1; return; }
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
        var escapedChar = new EscapedChar(input);
        index = 1;
        Length = escapedChar.Length;
        if (Length >= 0) return;
        var unicodeEscapeChar = new UnicodeEscapeChar(input);
        index = 2;
        Length = unicodeEscapeChar.Length;
        if (Length >= 0) return;
        var plainStringChar = new PlainStringChar(input);
        index = 3;
        Length = plainStringChar.Length;
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
            { Length = -1; return; }
        pos += 1;
        while (true)
        {
            var nextStringChar = new StringChar(input[pos..]);
            if (nextStringChar.Length < 0) break;
            pos += nextStringChar.Length;
        }
        if (pos >= input.Length || input[pos] != '\"')
            { Length = -1; return; }
        pos += 1;
        Length = pos;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var pos = 0;
        while (pos < Length)
        {
            var nextStringChar = new StringChar(input[pos..], input, 17);
            if (nextStringChar.Length < 0) break;
            visitor.Visit(nextStringChar);
            pos += nextStringChar.Length;
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
            { Length = -1; return; }
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
        var escapedChar = new EscapedChar(input);
        index = 1;
        Length = escapedChar.Length;
        if (Length >= 0) return;
        var unicodeEscapeChar = new UnicodeEscapeChar(input);
        index = 2;
        Length = unicodeEscapeChar.Length;
        if (Length >= 0) return;
        var plainChar = new PlainChar(input);
        index = 3;
        Length = plainChar.Length;
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
            { Length = -1; return; }
        pos += 1;
        charLiteralContentStart = pos;
        var charLiteralContent = new CharLiteralContent(input[pos..]);
        if (charLiteralContent.Length < 0) { Length = -1; return; }
        pos += charLiteralContent.Length;
        if (pos >= input.Length || input[pos] != '\'')
            { Length = -1; return; }
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
            { Length = -1; return; }
        i++;
        if (i >= input.Length || !IsNot0AChar(input[i]))
            { Length = -1; return; }
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
            { Length = -1; return; }
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
        var classEscapedChar = new ClassEscapedChar(input);
        index = 1;
        Length = classEscapedChar.Length;
        if (Length >= 0) return;
        var classLetterOrDigit = new ClassLetterOrDigit(input);
        index = 2;
        Length = classLetterOrDigit.Length;
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
        if (left.Length < 0) { Length = -1; return; }
        pos += left.Length;
        if (pos >= input.Length || input[pos] != '-')
            { Length = -1; return; }
        pos += 1;
        rightStart = pos;
        var right = new ClassChar(input[pos..]);
        if (right.Length < 0) { Length = -1; return; }
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
            { Length = -1; return; }
        i++;
        if (i >= input.Length || !IsWordWDigitDSpaceSChar(input[i]))
            { Length = -1; return; }
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

// PropertyName -> [A-Za-z_] [A-Za-z_0-9=]
public readonly ref struct PropertyName : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public PropertyName(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var i = 0;
        if (i >= input.Length || !IsAToZAToZ5FChar(input[i]))
            { Length = -1; return; }
        i++;
        while (i < input.Length && IsAToZAToZ5F0To93DChar(input[i]))
            i++;
        Length = i;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsAToZAToZ5FChar(Char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';

    private static Boolean IsAToZAToZ5F0To93DChar(Char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_' || (c >= '0' && c <= '9') || c == '=';
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
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != 'p')
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != '{')
            { Length = -1; return; }
        pos += 1;
        propertyNameStart = pos;
        var propertyName = new PropertyName(input[pos..]);
        if (propertyName.Length < 0) { Length = -1; return; }
        pos += propertyName.Length;
        if (pos >= input.Length || input[pos] != '}')
            { Length = -1; return; }
        pos += 1;
        Length = pos;
    }

    public PropertyName PropertyName => new(input[propertyNameStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new PropertyName(input[propertyNameStart..], input, 27));
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
        if (classChar.Length < 0) { Length = -1; return; }
        pos += classChar.Length;
        Length = pos;
    }

    public ClassChar ClassChar => new(input[classCharStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new ClassChar(input[classCharStart..], input, 28));
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
        var unicodePropertyClass = new UnicodePropertyClass(input);
        index = 3;
        Length = unicodePropertyClass.Length;
        if (Length >= 0) return;
        var charRange = new CharRange(input);
        index = 1;
        Length = charRange.Length;
        if (Length >= 0) return;
        var shorthandClass = new ShorthandClass(input);
        index = 2;
        Length = shorthandClass.Length;
        if (Length >= 0) return;
        var singleClassChar = new SingleClassChar(input);
        index = 4;
        Length = singleClassChar.Length;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new CharRange(input, input, 29)); break;
            case 2: visitor.Visit(new ShorthandClass(input, input, 29)); break;
            case 3: visitor.Visit(new UnicodePropertyClass(input, input, 29)); break;
            case 4: visitor.Visit(new SingleClassChar(input, input, 29)); break;
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
            { Length = -1; return; }
        pos += 1;
        if (pos < input.Length && input[pos] == '^')
            pos += 1;
        var firstClassRange = new ClassRange(input[pos..]);
        if (firstClassRange.Length < 0) { Length = -1; return; }
        pos += firstClassRange.Length;
        while (true)
        {
            var nextClassRange = new ClassRange(input[pos..]);
            if (nextClassRange.Length < 0) break;
            pos += nextClassRange.Length;
        }
        if (pos >= input.Length || input[pos] != ']')
            { Length = -1; return; }
        pos += 1;
        Length = pos;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var pos = 0;
        while (pos < Length)
        {
            var nextClassRange = new ClassRange(input[pos..], input, 30);
            if (nextClassRange.Length < 0) break;
            visitor.Visit(nextClassRange);
            pos += nextClassRange.Length;
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
        var bracketedClass = new BracketedClass(input);
        index = 1;
        Length = bracketedClass.Length;
        if (Length >= 0) return;
        var classRange = new ClassRange(input);
        index = 2;
        Length = classRange.Length;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new BracketedClass(input, input, 31)); break;
            case 2: visitor.Visit(new ClassRange(input, input, 31)); break;
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
        var group = new Group(input);
        index = 1;
        Length = group.Length;
        if (Length >= 0) return;
        var stringLiteral = new StringLiteral(input);
        index = 3;
        Length = stringLiteral.Length;
        if (Length >= 0) return;
        var charLiteral = new CharLiteral(input);
        index = 4;
        Length = charLiteral.Length;
        if (Length >= 0) return;
        var ruleRef = new RuleRef(input);
        index = 2;
        Length = ruleRef.Length;
        if (Length >= 0) return;
        var @class = new Class(input);
        index = 5;
        Length = @class.Length;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new Group(input, input, 32)); break;
            case 2: visitor.Visit(new RuleRef(input, input, 32)); break;
            case 3: visitor.Visit(new StringLiteral(input, input, 32)); break;
            case 4: visitor.Visit(new CharLiteral(input, input, 32)); break;
            case 5: visitor.Visit(new Class(input, input, 32)); break;
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

    public ZeroOrOne(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length < 1 || input[..1] is not "?")
            { Length = -1; return; }
        Length = 1;
    }

    public Input Text => input[..Length];
    public Int32 Length { get; }

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public static void Parse<T>(Input input, T visitor) where T : IVisitor
    {
        switch (input)
        {
            case "?": visitor.Visit(new ZeroOrOne(input)); break;
            default: visitor.Visit(new ParseError()); break;
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in ZeroOrOne zeroOrOne);
    }
}

// ZeroOrMore -> '*'
public readonly ref struct ZeroOrMore : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;

    public ZeroOrMore(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length < 1 || input[..1] is not "*")
            { Length = -1; return; }
        Length = 1;
    }

    public Input Text => input[..Length];
    public Int32 Length { get; }

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public static void Parse<T>(Input input, T visitor) where T : IVisitor
    {
        switch (input)
        {
            case "*": visitor.Visit(new ZeroOrMore(input)); break;
            default: visitor.Visit(new ParseError()); break;
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in ZeroOrMore zeroOrMore);
    }
}

// OneOrMore -> '+'
public readonly ref struct OneOrMore : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;

    public OneOrMore(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length < 1 || input[..1] is not "+")
            { Length = -1; return; }
        Length = 1;
    }

    public Input Text => input[..Length];
    public Int32 Length { get; }

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    public static void Parse<T>(Input input, T visitor) where T : IVisitor
    {
        switch (input)
        {
            case "+": visitor.Visit(new OneOrMore(input)); break;
            default: visitor.Visit(new ParseError()); break;
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in OneOrMore oneOrMore);
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
            { Length = -1; return; }
        pos += 1;
        numberStart = pos;
        var number = new Number(input[pos..]);
        if (number.Length < 0) { Length = -1; return; }
        pos += number.Length;
        if (pos >= input.Length || input[pos] != '}')
            { Length = -1; return; }
        pos += 1;
        Length = pos;
    }

    public Number Number => new(input[numberStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Number(input[numberStart..], input, 36));
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
            { Length = -1; return; }
        pos += 1;
        numberStart = pos;
        var number = new Number(input[pos..]);
        if (number.Length < 0) { Length = -1; return; }
        pos += number.Length;
        if (pos >= input.Length || input[pos] != ',')
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != '}')
            { Length = -1; return; }
        pos += 1;
        Length = pos;
    }

    public Number Number => new(input[numberStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Number(input[numberStart..], input, 37));
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
            { Length = -1; return; }
        pos += 1;
        leftStart = pos;
        var left = new Number(input[pos..]);
        if (left.Length < 0) { Length = -1; return; }
        pos += left.Length;
        if (pos >= input.Length || input[pos] != ',')
            { Length = -1; return; }
        pos += 1;
        rightStart = pos;
        var right = new Number(input[pos..]);
        if (right.Length < 0) { Length = -1; return; }
        pos += right.Length;
        if (pos >= input.Length || input[pos] != '}')
            { Length = -1; return; }
        pos += 1;
        Length = pos;
    }

    public Number Left => new(input[leftStart..]);
    public Number Right => new(input[rightStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Number(input[leftStart..], input, 38));
        visitor.Visit(new Number(input[rightStart..], input, 38));
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
        var between = new Between(input);
        index = 6;
        Length = between.Length;
        if (Length >= 0) return;
        var atLeast = new AtLeast(input);
        index = 5;
        Length = atLeast.Length;
        if (Length >= 0) return;
        var exactly = new Exactly(input);
        index = 4;
        Length = exactly.Length;
        if (Length >= 0) return;
        var zeroOrOne = new ZeroOrOne(input);
        index = 1;
        Length = zeroOrOne.Length;
        if (Length >= 0) return;
        var zeroOrMore = new ZeroOrMore(input);
        index = 2;
        Length = zeroOrMore.Length;
        if (Length >= 0) return;
        var oneOrMore = new OneOrMore(input);
        index = 3;
        Length = oneOrMore.Length;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new ZeroOrOne(input, input, 39)); break;
            case 2: visitor.Visit(new ZeroOrMore(input, input, 39)); break;
            case 3: visitor.Visit(new OneOrMore(input, input, 39)); break;
            case 4: visitor.Visit(new Exactly(input, input, 39)); break;
            case 5: visitor.Visit(new AtLeast(input, input, 39)); break;
            case 6: visitor.Visit(new Between(input, input, 39)); break;
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
        if (atom.Length < 0) { Length = -1; return; }
        pos += atom.Length;
        quantifierStart = pos;
        var quantifier = new Quantifier(input[pos..]);
        if (quantifier.Length >= 0) pos += quantifier.Length;
        Length = pos;
    }

    public Atom Atom => new(input[atomStart..]);
    public Quantifier Quantifier => new(input[quantifierStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Atom(input[atomStart..], input, 40));
        var quantifierOpt = new Quantifier(input[quantifierStart..], input, 40);
        if (quantifierOpt.Length >= 0) visitor.Visit(quantifierOpt);
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
        if (item.Length < 0) { Length = -1; return; }
        pos += item.Length;
        while (pos < input.Length && input[pos] == ' ')
        {
            pos += 1;
            var next = new Item(input[pos..]);
            if (next.Length < 0) break;
            pos += next.Length;
        }
        Length = pos;
    }

    public Item Item => new(input[itemStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var item = new Item(input[itemStart..], input, 41);
        visitor.Visit(item);
        var pos = itemStart + item.Length;
        while (pos < Length)
        {
            var savedPos = pos;
            if (pos >= input.Length || input[pos] != ' ') { pos = savedPos; break; }
            pos += 1;
            var nextItem = new Item(input[pos..], input, 41);
            if (nextItem.Length < 0) { pos = savedPos; break; }
            visitor.Visit(nextItem);
            pos += nextItem.Length;
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
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != '-')
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != '>')
            { Length = -1; return; }
        pos += 1;
        if (pos >= input.Length || input[pos] != ' ')
            { Length = -1; return; }
        pos += 1;
        alternativeStart = pos;
        var alternative = new Alternative(input[pos..]);
        if (alternative.Length < 0) { Length = -1; return; }
        pos += alternative.Length;
        Length = pos;
    }

    public Alternative Alternative => new(input[alternativeStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Alternative(input[alternativeStart..], input, 42));
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
        if (input.Length >= 4 && input[..4] is "    ")
        {
            Length = 4;
            return;
        }
        if (input.Length >= 1 && input[..1] is "\t")
        {
            Length = 1;
            return;
        }
        Length = -1;
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
            { Length = -1; return; }
        pos += 1;
        indentationStart = pos;
        var indentation = new Indentation(input[pos..]);
        if (indentation.Length < 0) { Length = -1; return; }
        pos += indentation.Length;
        alternativeStart = pos;
        var alternative = new Alternative(input[pos..]);
        if (alternative.Length < 0) { Length = -1; return; }
        pos += alternative.Length;
        Length = pos;
    }

    public Indentation Indentation => new(input[indentationStart..]);
    public Alternative Alternative => new(input[alternativeStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Indentation(input[indentationStart..], input, 44));
        visitor.Visit(new Alternative(input[alternativeStart..], input, 44));
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
        if (firstIndentedAlternative.Length < 0) { Length = -1; return; }
        pos += firstIndentedAlternative.Length;
        while (true)
        {
            var nextIndentedAlternative = new IndentedAlternative(input[pos..]);
            if (nextIndentedAlternative.Length < 0) break;
            pos += nextIndentedAlternative.Length;
        }
        Length = pos;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var pos = 0;
        while (pos < Length)
        {
            var nextIndentedAlternative = new IndentedAlternative(input[pos..], input, 45);
            if (nextIndentedAlternative.Length < 0) break;
            visitor.Visit(nextIndentedAlternative);
            pos += nextIndentedAlternative.Length;
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
        var inlineAlternatives = new InlineAlternatives(input);
        index = 1;
        Length = inlineAlternatives.Length;
        if (Length >= 0) return;
        var indentedAlternatives = new IndentedAlternatives(input);
        index = 2;
        Length = indentedAlternatives.Length;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new InlineAlternatives(input, input, 46)); break;
            case 2: visitor.Visit(new IndentedAlternatives(input, input, 46)); break;
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
        if (name.Length < 0) { Length = -1; return; }
        pos += name.Length;
        alternativesStart = pos;
        var alternatives = new Alternatives(input[pos..]);
        if (alternatives.Length < 0) { Length = -1; return; }
        pos += alternatives.Length;
        Length = pos;
    }

    public Name Name => new(input[nameStart..]);
    public Alternatives Alternatives => new(input[alternativesStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(new Name(input[nameStart..], input, 47));
        visitor.Visit(new Alternatives(input[alternativesStart..], input, 47));
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
        if (rule.Length < 0) { Length = -1; return; }
        pos += rule.Length;
        while (true)
        {
            var savedPos = pos;
            if (!input[pos..].StartsWith("\n"))
                { pos = savedPos; break; }
            pos += 1;
            if (!input[pos..].StartsWith("\n"))
                { pos = savedPos; break; }
            pos += 1;
            var nextRule = new Rule(input[pos..]);
            if (nextRule.Length < 0) { pos = savedPos; break; }
            pos += nextRule.Length;
        }
        Length = pos;
    }

    public Rule Rule => new(input[ruleStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var rule = new Rule(input[ruleStart..], input, 48);
        visitor.Visit(rule);
        var pos = ruleStart + rule.Length;
        while (pos < Length)
        {
            var savedPos = pos;
            if (pos >= input.Length || input[pos] != '\n') { pos = savedPos; break; }
            pos += 1;
            if (pos >= input.Length || input[pos] != '\n') { pos = savedPos; break; }
            pos += 1;
            while (pos < input.Length && input[pos] == '\n')
                pos += 1;
            var nextRule = new Rule(input[pos..], input, 48);
            if (nextRule.Length < 0) { pos = savedPos; break; }
            visitor.Visit(nextRule);
            pos += nextRule.Length;
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
            case 26: visitor.Visit(new PropertyName(input)); break;
            case 27: visitor.Visit(new UnicodePropertyClass(input)); break;
            case 28: visitor.Visit(new SingleClassChar(input)); break;
            case 29: visitor.Visit(new ClassRange(input)); break;
            case 30: visitor.Visit(new BracketedClass(input)); break;
            case 31: visitor.Visit(new Class(input)); break;
            case 32: visitor.Visit(new Atom(input)); break;
            case 33: visitor.Visit(new ZeroOrOne(input)); break;
            case 34: visitor.Visit(new ZeroOrMore(input)); break;
            case 35: visitor.Visit(new OneOrMore(input)); break;
            case 36: visitor.Visit(new Exactly(input)); break;
            case 37: visitor.Visit(new AtLeast(input)); break;
            case 38: visitor.Visit(new Between(input)); break;
            case 39: visitor.Visit(new Quantifier(input)); break;
            case 40: visitor.Visit(new Item(input)); break;
            case 41: visitor.Visit(new Alternative(input)); break;
            case 42: visitor.Visit(new InlineAlternatives(input)); break;
            case 43: visitor.Visit(new Indentation(input)); break;
            case 44: visitor.Visit(new IndentedAlternative(input)); break;
            case 45: visitor.Visit(new IndentedAlternatives(input)); break;
            case 46: visitor.Visit(new Alternatives(input)); break;
            case 47: visitor.Visit(new Rule(input)); break;
            case 48: visitor.Visit(new Grammar(input)); break;
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

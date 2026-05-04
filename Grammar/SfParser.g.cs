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

// Name -> [\w] [\w\d]
public readonly ref struct Name : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public Name(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        var i = 0;
        if (i >= input.Length || !IsWordChar(input[i]))
            throw new ParseException(new ParseError());
        i++;
        while (i < input.Length && IsWordDigitChar(input[i]))
            i++;
        Length = i;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }

    private static Boolean IsWordChar(Char c) =>
        (Char.IsLetter(c) || c == '_');

    private static Boolean IsWordDigitChar(Char c) =>
        (Char.IsLetter(c) || c == '_') || Char.IsAsciiDigit(c);
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
        visitor.Visit(new Alternative(input[alternativeStart..], input, 2));
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
        visitor.Visit(new Name(input[nameStart..], input, 3));
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
            var plainStringChar = new PlainStringChar(input);
            index = 2;
            Length = plainStringChar.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new EscapedChar(input, input, 6)); break;
            case 2: visitor.Visit(new PlainStringChar(input, input, 6)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new EscapedChar(input)); break;
            case 2: visitor.Visit(new PlainStringChar(input)); break;
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
                var nextStringChar = new StringChar(input[pos..], input, 7);
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
            var plainChar = new PlainChar(input);
            index = 2;
            Length = plainChar.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new EscapedChar(input, input, 9)); break;
            case 2: visitor.Visit(new PlainChar(input, input, 9)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new EscapedChar(input)); break;
            case 2: visitor.Visit(new PlainChar(input)); break;
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
        visitor.Visit(new CharLiteralContent(input[charLiteralContentStart..], input, 10));
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
            case 1: visitor.Visit(new ClassEscapedChar(input, input, 13)); break;
            case 2: visitor.Visit(new ClassLetterOrDigit(input, input, 13)); break;
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
        visitor.Visit(new ClassChar(input[leftStart..], input, 14));
        visitor.Visit(new ClassChar(input[rightStart..], input, 14));
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
        visitor.Visit(new ClassChar(input[classCharStart..], input, 16));
    }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
    }
}

// ClassRange
//     CharRange
//     ShorthandClass
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
                index = 3;
                Length = singleClassChar.Length;
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : SfParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new CharRange(input, input, 17)); break;
            case 2: visitor.Visit(new ShorthandClass(input, input, 17)); break;
            case 3: visitor.Visit(new SingleClassChar(input, input, 17)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new CharRange(input)); break;
            case 2: visitor.Visit(new ShorthandClass(input)); break;
            case 3: visitor.Visit(new SingleClassChar(input)); break;
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
        void Visit(in SingleClassChar singleClassChar);
    }
}

// Class -> '[' '^' ClassRange+ ']'
public readonly ref struct Class : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public Class(Input input, Input parentInput = default, Byte parentKind = 0)
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
                var nextClassRange = new ClassRange(input[pos..], input, 18);
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
            var @class = new Class(input);
            index = 5;
            Length = @class.Length;
        }
        catch (ParseException)
        {
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
                        var ruleRef = new RuleRef(input);
                        index = 2;
                        Length = ruleRef.Length;
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
            case 1: visitor.Visit(new Group(input, input, 19)); break;
            case 2: visitor.Visit(new RuleRef(input, input, 19)); break;
            case 3: visitor.Visit(new StringLiteral(input, input, 19)); break;
            case 4: visitor.Visit(new CharLiteral(input, input, 19)); break;
            case 5: visitor.Visit(new Class(input, input, 19)); break;
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

// Quantifier
//     '?'
//     '*'
//     '+'
public readonly ref struct Quantifier : IRule
{
    private readonly Input input;
    private readonly Input parentInput;
    private readonly Byte parentKind;
    public Int32 Length { get; }

    public Quantifier(Input input, Input parentInput = default, Byte parentKind = 0)
    {
        this.input = input;
        this.parentInput = parentInput;
        this.parentKind = parentKind;
        if (input.Length > 0 && input[0] == '?')
        {
            if (input.Length == 0 || input[0] != '?')
                throw new ParseException(new ParseError());
            Length = 1;
        }
        else if (input.Length > 0 && input[0] == '*')
        {
            if (input.Length == 0 || input[0] != '*')
                throw new ParseException(new ParseError());
            Length = 1;
        }
        else
        {
            if (input.Length == 0 || input[0] != '+')
                throw new ParseException(new ParseError());
            Length = 1;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    public void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        Rules.Visit(parentKind, parentInput, ref visitor);
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
        visitor.Visit(new Atom(input[atomStart..], input, 21));
        try
        {
            visitor.Visit(new Quantifier(input[quantifierStart..], input, 21));
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
        var item = new Item(input[itemStart..], input, 22);
        visitor.Visit(item);
        var pos = itemStart + item.Length;
        while (pos < Length)
        {
            try
            {
                if (pos >= input.Length || input[pos] != ' ') break;
                pos += 1;
                var nextItem = new Item(input[pos..], input, 22);
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
        visitor.Visit(new Alternative(input[alternativeStart..], input, 23));
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
        visitor.Visit(new Indentation(input[indentationStart..], input, 25));
        visitor.Visit(new Alternative(input[alternativeStart..], input, 25));
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
                var nextIndentedAlternative = new IndentedAlternative(input[pos..], input, 26);
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
            case 1: visitor.Visit(new InlineAlternatives(input, input, 27)); break;
            case 2: visitor.Visit(new IndentedAlternatives(input, input, 27)); break;
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
        visitor.Visit(new Name(input[nameStart..], input, 28));
        visitor.Visit(new Alternatives(input[alternativesStart..], input, 28));
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
        var rule = new Rule(input[ruleStart..], input, 29);
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
                var nextRule = new Rule(input[pos..], input, 29);
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
            case 1: visitor.Visit(new Name(input)); break;
            case 2: visitor.Visit(new Group(input)); break;
            case 3: visitor.Visit(new RuleRef(input)); break;
            case 4: visitor.Visit(new EscapedChar(input)); break;
            case 5: visitor.Visit(new PlainStringChar(input)); break;
            case 6: visitor.Visit(new StringChar(input)); break;
            case 7: visitor.Visit(new StringLiteral(input)); break;
            case 8: visitor.Visit(new PlainChar(input)); break;
            case 9: visitor.Visit(new CharLiteralContent(input)); break;
            case 10: visitor.Visit(new CharLiteral(input)); break;
            case 11: visitor.Visit(new ClassEscapedChar(input)); break;
            case 12: visitor.Visit(new ClassLetterOrDigit(input)); break;
            case 13: visitor.Visit(new ClassChar(input)); break;
            case 14: visitor.Visit(new CharRange(input)); break;
            case 15: visitor.Visit(new ShorthandClass(input)); break;
            case 16: visitor.Visit(new SingleClassChar(input)); break;
            case 17: visitor.Visit(new ClassRange(input)); break;
            case 18: visitor.Visit(new Class(input)); break;
            case 19: visitor.Visit(new Atom(input)); break;
            case 20: visitor.Visit(new Quantifier(input)); break;
            case 21: visitor.Visit(new Item(input)); break;
            case 22: visitor.Visit(new Alternative(input)); break;
            case 23: visitor.Visit(new InlineAlternatives(input)); break;
            case 24: visitor.Visit(new Indentation(input)); break;
            case 25: visitor.Visit(new IndentedAlternative(input)); break;
            case 26: visitor.Visit(new IndentedAlternatives(input)); break;
            case 27: visitor.Visit(new Alternatives(input)); break;
            case 28: visitor.Visit(new Rule(input)); break;
            case 29: visitor.Visit(new Grammar(input)); break;
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

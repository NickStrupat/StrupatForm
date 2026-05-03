using Input = System.ReadOnlySpan<System.Char>;

namespace GeneratedParser;

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
}

public interface IVisitor
{
    void Visit<T>(T rule) where T : IRule, allows ref struct;
}

// Identifier -> [w] [wd]
public readonly ref struct Identifier : IRule
{
    private readonly Input input;
    public Int32 Length { get; }

    public Identifier(Input input)
    {
        this.input = input;
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

    private static Boolean IsWordChar(Char c) =>
        (Char.IsLetter(c) || c == '_');

    private static Boolean IsWordDigitChar(Char c) =>
        (Char.IsLetter(c) || c == '_') || Char.IsAsciiDigit(c);
}

// QualifiedIdentifier -> Identifier ('.' Identifier)*
public readonly ref struct QualifiedIdentifier : IRule
{
    private readonly Input input;
    private readonly Int32 identifierStart;
    public Int32 Length { get; }

    public QualifiedIdentifier(Input input)
    {
        this.input = input;
        var pos = 0;
        identifierStart = pos;
        var identifier = new Identifier(input[pos..]);
        pos += identifier.Length;
        pos += SkipWhitespace(input[pos..]);
        while (pos < input.Length && input[pos] == '.')
        {
            pos += 1;
            var next = new Identifier(input[pos..]);
            pos += next.Length;
        }
        Length = pos;
    }

    public Identifier Identifier => new(input[identifierStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var identifier = new Identifier(input[identifierStart..]);
        visitor.Visit(identifier);
        var pos = identifierStart + identifier.Length;
        while (pos < Length)
        {
            pos += SkipWhitespace(input[pos..]);
            var nextIdentifier = new Identifier(input[pos..]);
            visitor.Visit(nextIdentifier);
            pos += nextIdentifier.Length;
            pos += SkipWhitespace(input[pos..]);
        }
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// Literal
//     '"' [^"] '"'
//     [0-9] [0-9]
//     '0' [xX] [0-9a-fA-F]
public readonly ref struct Literal : IRule
{
    private readonly Input input;
    public Int32 Length { get; }

    public Literal(Input input)
    {
        this.input = input;
        if (input.Length > 0 && input[0] == '\"')
        {
            var end = input[1..].IndexOf('\"');
            if (end < 0)
                throw new ParseException(new ParseError());
            Length = end + 2;
        }
        else if (input.Length > 0 && ((input[0] >= '0' && input[0] <= '9')))
        {
            var i = 0;
            if (i >= input.Length || !Is0To9Char(input[i]))
                throw new ParseException(new ParseError());
            i++;
            while (i < input.Length && Is0To9Char(input[i]))
                i++;
            Length = i;
        }
        else
        {
            var i = 0;
            if (i >= input.Length || input[i] != '0')
                throw new ParseException(new ParseError());
            i++;
            if (i >= input.Length || !IsXXChar(input[i]))
                throw new ParseException(new ParseError());
            i++;
            if (i >= input.Length || !Is0To9AToFAToFChar(input[i]))
                throw new ParseException(new ParseError());
            i++;
            while (i < input.Length && Is0To9AToFAToFChar(input[i]))
                i++;
            Length = i;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    private static Boolean IsNot22Char(Char c) =>
        !(c == '\"');

    private static Boolean Is0To9Char(Char c) =>
        (c >= '0' && c <= '9');

    private static Boolean IsXXChar(Char c) =>
        c == 'x' || c == 'X';

    private static Boolean Is0To9AToFAToFChar(Char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}

// PrimaryExpression
//     Identifier
//     Literal
public readonly ref struct PrimaryExpression : IRule
{
    private readonly Input input;
    private readonly Byte index;
    public Int32 Length { get; }

    public PrimaryExpression(Input input)
    {
        this.input = input;
        try
        {
            var literal = new Literal(input);
            index = 2;
            Length = literal.Length;
        }
        catch (ParseException)
        {
            var identifier = new Identifier(input);
            index = 1;
            Length = identifier.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : GeneratedParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new Identifier(input)); break;
            case 2: visitor.Visit(new Literal(input)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new Identifier(input)); break;
            case 2: visitor.Visit(new Literal(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in Identifier identifier);
        void Visit(in Literal literal);
    }
}

// UnaryOperator -> "!"
public readonly ref struct UnaryOperator : IRule
{
    public UnaryOperator(Input input)
    {
        if (input.Length < 1 || input[..1] is not "!")
            throw new ParseException(new ParseError());
        Text = input[..1];
    }

    public Input Text { get; }
    public Int32 Length => 1;

    public void VisitChildren<T>(ref T visitor) where T : GeneratedParser.IVisitor, allows ref struct { }

    public static void Parse<T>(Input input, T visitor) where T : IVisitor
    {
        switch (input)
        {
            case "!": visitor.Visit(new UnaryOperator(input)); break;
            default: visitor.Visit(new ParseError()); break;
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in UnaryOperator unaryOperator);
    }
}

// UnaryExpression -> UnaryOperator Expression
public readonly ref struct UnaryExpression : IRule
{
    private readonly Input input;
    private readonly Int32 unaryOperatorStart;
    private readonly Int32 expressionStart;
    public Int32 Length { get; }

    public UnaryExpression(Input input)
    {
        this.input = input;
        var pos = 0;
        unaryOperatorStart = pos;
        var unaryOperator = new UnaryOperator(input[pos..]);
        pos += unaryOperator.Length;
        pos += SkipWhitespace(input[pos..]);
        expressionStart = pos;
        var expression = new Expression(input[pos..]);
        pos += expression.Length;
        Length = pos;
    }

    public UnaryOperator UnaryOperator => new(input[unaryOperatorStart..]);
    public Expression Expression => new(input[expressionStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(UnaryOperator);
        visitor.Visit(Expression);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// Atom
//     PrimaryExpression
//     UnaryExpression
public readonly ref struct Atom : IRule
{
    private readonly Input input;
    private readonly Byte index;
    public Int32 Length { get; }

    public Atom(Input input)
    {
        this.input = input;
        try
        {
            var unaryExpression = new UnaryExpression(input);
            index = 2;
            Length = unaryExpression.Length;
        }
        catch (ParseException)
        {
            var primaryExpression = new PrimaryExpression(input);
            index = 1;
            Length = primaryExpression.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : GeneratedParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new PrimaryExpression(input)); break;
            case 2: visitor.Visit(new UnaryExpression(input)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new PrimaryExpression(input)); break;
            case 2: visitor.Visit(new UnaryExpression(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in PrimaryExpression primaryExpression);
        void Visit(in UnaryExpression unaryExpression);
    }
}

// Expression -> Atom (BinaryOperator Atom)*
public readonly ref struct Expression : IRule
{
    private readonly Input input;
    private readonly Int32 atomStart;
    public Int32 Length { get; }

    public Expression(Input input)
    {
        this.input = input;
        var pos = 0;
        atomStart = pos;
        var atom = new Atom(input[pos..]);
        pos += atom.Length;
        pos += SkipWhitespace(input[pos..]);
        while (true)
        {
            var savedPos = pos;
            try
            {
                pos += SkipWhitespace(input[pos..]);
                var nextBinaryOperator = new BinaryOperator(input[pos..]);
                pos += nextBinaryOperator.Length;
                pos += SkipWhitespace(input[pos..]);
                var nextAtom = new Atom(input[pos..]);
                pos += nextAtom.Length;
                pos += SkipWhitespace(input[pos..]);
            }
            catch (ParseException)
            {
                pos = savedPos;
                break;
            }
        }
        Length = pos;
    }

    public Atom Atom => new(input[atomStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        var atom = new Atom(input[atomStart..]);
        visitor.Visit(atom);
        var pos = atomStart + atom.Length;
        while (pos < Length)
        {
            pos += SkipWhitespace(input[pos..]);
            var nextBinaryOperator = new BinaryOperator(input[pos..]);
            visitor.Visit(nextBinaryOperator);
            pos += nextBinaryOperator.Length;
            pos += SkipWhitespace(input[pos..]);
            var nextAtom = new Atom(input[pos..]);
            visitor.Visit(nextAtom);
            pos += nextAtom.Length;
            pos += SkipWhitespace(input[pos..]);
        }
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// ExpressionStatement -> Expression
public readonly ref struct ExpressionStatement : IRule
{
    private readonly Input input;
    private readonly Int32 expressionStart;
    public Int32 Length { get; }

    public ExpressionStatement(Input input)
    {
        this.input = input;
        var pos = 0;
        expressionStart = pos;
        var expression = new Expression(input[pos..]);
        pos += expression.Length;
        Length = pos;
    }

    public Expression Expression => new(input[expressionStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Expression);
    }
}

// VariableDeclarationStatement -> "var" Identifier "=" Expression
public readonly ref struct VariableDeclarationStatement : IRule
{
    private readonly Input input;
    private readonly Int32 identifierStart;
    private readonly Int32 expressionStart;
    public Int32 Length { get; }

    public VariableDeclarationStatement(Input input)
    {
        this.input = input;
        var pos = 0;
        if (!input[pos..].StartsWith("var"))
            throw new ParseException(new ParseError());
        pos += 3;
        pos += SkipWhitespace(input[pos..]);
        identifierStart = pos;
        var identifier = new Identifier(input[pos..]);
        pos += identifier.Length;
        pos += SkipWhitespace(input[pos..]);
        if (!input[pos..].StartsWith("="))
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        expressionStart = pos;
        var expression = new Expression(input[pos..]);
        pos += expression.Length;
        Length = pos;
    }

    public Identifier Identifier => new(input[identifierStart..]);
    public Expression Expression => new(input[expressionStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Identifier);
        visitor.Visit(Expression);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// AssignmentStatement -> Identifier "=" Expression
public readonly ref struct AssignmentStatement : IRule
{
    private readonly Input input;
    private readonly Int32 identifierStart;
    private readonly Int32 expressionStart;
    public Int32 Length { get; }

    public AssignmentStatement(Input input)
    {
        this.input = input;
        var pos = 0;
        identifierStart = pos;
        var identifier = new Identifier(input[pos..]);
        pos += identifier.Length;
        pos += SkipWhitespace(input[pos..]);
        if (!input[pos..].StartsWith("="))
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        expressionStart = pos;
        var expression = new Expression(input[pos..]);
        pos += expression.Length;
        Length = pos;
    }

    public Identifier Identifier => new(input[identifierStart..]);
    public Expression Expression => new(input[expressionStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Identifier);
        visitor.Visit(Expression);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// ReturnStatement -> "return" Expression
public readonly ref struct ReturnStatement : IRule
{
    private readonly Input input;
    private readonly Int32 expressionStart;
    public Int32 Length { get; }

    public ReturnStatement(Input input)
    {
        this.input = input;
        var pos = 0;
        if (!input[pos..].StartsWith("return"))
            throw new ParseException(new ParseError());
        pos += 6;
        pos += SkipWhitespace(input[pos..]);
        expressionStart = pos;
        var expression = new Expression(input[pos..]);
        pos += expression.Length;
        Length = pos;
    }

    public Expression Expression => new(input[expressionStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Expression);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// Statement
//     ExpressionStatement
//     VariableDeclarationStatement
//     AssignmentStatement
//     ReturnStatement
public readonly ref struct Statement : IRule
{
    private readonly Input input;
    private readonly Byte index;
    public Int32 Length { get; }

    public Statement(Input input)
    {
        this.input = input;
        if (input.StartsWith("var"))
        {
            var variableDeclarationStatement = new VariableDeclarationStatement(input);
            index = 2;
            Length = variableDeclarationStatement.Length;
        }
        else if (input.StartsWith("return"))
        {
            var returnStatement = new ReturnStatement(input);
            index = 4;
            Length = returnStatement.Length;
        }
        else
        {
            try
            {
                var assignmentStatement = new AssignmentStatement(input);
                index = 3;
                Length = assignmentStatement.Length;
            }
            catch (ParseException)
            {
                var expressionStatement = new ExpressionStatement(input);
                index = 1;
                Length = expressionStatement.Length;
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : GeneratedParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new ExpressionStatement(input)); break;
            case 2: visitor.Visit(new VariableDeclarationStatement(input)); break;
            case 3: visitor.Visit(new AssignmentStatement(input)); break;
            case 4: visitor.Visit(new ReturnStatement(input)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new ExpressionStatement(input)); break;
            case 2: visitor.Visit(new VariableDeclarationStatement(input)); break;
            case 3: visitor.Visit(new AssignmentStatement(input)); break;
            case 4: visitor.Visit(new ReturnStatement(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in ExpressionStatement expressionStatement);
        void Visit(in VariableDeclarationStatement variableDeclarationStatement);
        void Visit(in AssignmentStatement assignmentStatement);
        void Visit(in ReturnStatement returnStatement);
    }
}

// BodyElement -> Statement ';'
public readonly ref struct BodyElement : IRule
{
    private readonly Input input;
    private readonly Int32 statementStart;
    public Int32 Length { get; }

    public BodyElement(Input input)
    {
        this.input = input;
        var pos = 0;
        statementStart = pos;
        var statement = new Statement(input[pos..]);
        pos += statement.Length;
        pos += SkipWhitespace(input[pos..]);
        if (input[pos] != ';')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Statement Statement => new(input[statementStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Statement);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// FunctionBody
//     Statement
//     BodyElement+
public readonly ref struct FunctionBody : IRule
{
    private readonly Input input;
    private readonly Byte index;
    public Int32 Length { get; }

    public FunctionBody(Input input)
    {
        this.input = input;
        try
        {
            var first = new BodyElement(input);
            var pos = first.Length;
            pos += SkipWhitespace(input[pos..]);
            while (pos < input.Length)
            {
                try
                {
                    var next = new BodyElement(input[pos..]);
                    pos += next.Length;
                    pos += SkipWhitespace(input[pos..]);
                }
                catch (ParseException)
                {
                    break;
                }
            }
            index = 2;
            Length = pos;
        }
        catch (ParseException)
        {
            var statement = new Statement(input);
            index = 1;
            Length = statement.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : GeneratedParser.IVisitor, allows ref struct
    {
        if (index == 1)
        {
            visitor.Visit(new Statement(input));
        }
        else if (index == 2)
        {
            foreach (var elem in new BodyElementEnumerable(input[..Length]))
                visitor.Visit(elem);
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new Statement(input)); break;
            case 2: visitor.Visit(new BodyElementEnumerable(input[..Length])); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in Statement statement);
        void Visit(in BodyElementEnumerable bodyElements);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// BodyElement+ enumerable
public readonly ref struct BodyElementEnumerable
{
    private readonly Input input;

    public BodyElementEnumerable(Input input) => this.input = input;

    public Enumerator GetEnumerator() => new(input);

    public ref struct Enumerator
    {
        private readonly Input input;
        private Int32 consumed;

        public Enumerator(Input input) => this.input = input;

        public BodyElement Current { get; private set; }

        public Boolean MoveNext()
        {
            var remaining = input[consumed..];
            var ws = SkipWhitespace(remaining);
            remaining = remaining[ws..];
            if (remaining.IsEmpty)
                return false;
            try
            {
                Current = new BodyElement(remaining);
                consumed += ws + Current.Length;
                return true;
            }
            catch (ParseException)
            {
                return false;
            }
        }

        private static Int32 SkipWhitespace(Input input)
        {
            var i = 0;
            while (i < input.Length && Char.IsWhiteSpace(input[i]))
                i++;
            return i;
        }
    }
}

// FunctionDeclaration -> "function" Identifier "{" FunctionBody "}"
public readonly ref struct FunctionDeclaration : IRule
{
    private readonly Input input;
    private readonly Int32 identifierStart;
    private readonly Int32 functionBodyStart;
    public Int32 Length { get; }

    public FunctionDeclaration(Input input)
    {
        this.input = input;
        var pos = 0;
        if (!input[pos..].StartsWith("function"))
            throw new ParseException(new ParseError());
        pos += 8;
        pos += SkipWhitespace(input[pos..]);
        identifierStart = pos;
        var identifier = new Identifier(input[pos..]);
        pos += identifier.Length;
        pos += SkipWhitespace(input[pos..]);
        if (!input[pos..].StartsWith("{"))
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        functionBodyStart = pos;
        var functionBody = new FunctionBody(input[pos..]);
        pos += functionBody.Length;
        pos += SkipWhitespace(input[pos..]);
        if (!input[pos..].StartsWith("}"))
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Identifier Identifier => new(input[identifierStart..]);
    public FunctionBody FunctionBody => new(input[functionBodyStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Identifier);
        visitor.Visit(FunctionBody);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// TypeBody
//     TypeDeclaration
//     FunctionDeclaration
public readonly ref struct TypeBody : IRule
{
    private readonly Input input;
    private readonly Byte index;
    public Int32 Length { get; }

    public TypeBody(Input input)
    {
        this.input = input;
        if (input.StartsWith("type"))
        {
            var typeDeclaration = new TypeDeclaration(input);
            index = 1;
            Length = typeDeclaration.Length;
        }
        else if (input.StartsWith("function"))
        {
            var functionDeclaration = new FunctionDeclaration(input);
            index = 2;
            Length = functionDeclaration.Length;
        }
        else
        {
            index = Byte.MaxValue;
            Length = 0;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : GeneratedParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new TypeDeclaration(input)); break;
            case 2: visitor.Visit(new FunctionDeclaration(input)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new TypeDeclaration(input)); break;
            case 2: visitor.Visit(new FunctionDeclaration(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in TypeDeclaration typeDeclaration);
        void Visit(in FunctionDeclaration functionDeclaration);
    }
}

// TypeDeclaration -> "type" Identifier "{" TypeBody "}"
public readonly ref struct TypeDeclaration : IRule
{
    private readonly Input input;
    private readonly Int32 identifierStart;
    private readonly Int32 typeBodyStart;
    public Int32 Length { get; }

    public TypeDeclaration(Input input)
    {
        this.input = input;
        var pos = 0;
        if (!input[pos..].StartsWith("type"))
            throw new ParseException(new ParseError());
        pos += 4;
        pos += SkipWhitespace(input[pos..]);
        identifierStart = pos;
        var identifier = new Identifier(input[pos..]);
        pos += identifier.Length;
        pos += SkipWhitespace(input[pos..]);
        if (!input[pos..].StartsWith("{"))
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        typeBodyStart = pos;
        var typeBody = new TypeBody(input[pos..]);
        pos += typeBody.Length;
        pos += SkipWhitespace(input[pos..]);
        if (!input[pos..].StartsWith("}"))
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Identifier Identifier => new(input[identifierStart..]);
    public TypeBody TypeBody => new(input[typeBodyStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Identifier);
        visitor.Visit(TypeBody);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// NamespaceBody
//     NamespaceDeclaration
//     TypeDeclaration
//     FunctionDeclaration
public readonly ref struct NamespaceBody : IRule
{
    private readonly Input input;
    private readonly Byte index;
    public Int32 Length { get; }

    public NamespaceBody(Input input)
    {
        this.input = input;
        if (input.StartsWith("namespace"))
        {
            var namespaceDeclaration = new NamespaceDeclaration(input);
            index = 1;
            Length = namespaceDeclaration.Length;
        }
        else if (input.StartsWith("type"))
        {
            var typeDeclaration = new TypeDeclaration(input);
            index = 2;
            Length = typeDeclaration.Length;
        }
        else if (input.StartsWith("function"))
        {
            var functionDeclaration = new FunctionDeclaration(input);
            index = 3;
            Length = functionDeclaration.Length;
        }
        else
        {
            index = Byte.MaxValue;
            Length = 0;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : GeneratedParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new NamespaceDeclaration(input)); break;
            case 2: visitor.Visit(new TypeDeclaration(input)); break;
            case 3: visitor.Visit(new FunctionDeclaration(input)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new NamespaceDeclaration(input)); break;
            case 2: visitor.Visit(new TypeDeclaration(input)); break;
            case 3: visitor.Visit(new FunctionDeclaration(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in NamespaceDeclaration namespaceDeclaration);
        void Visit(in TypeDeclaration typeDeclaration);
        void Visit(in FunctionDeclaration functionDeclaration);
    }
}

// NamespaceDeclaration -> "namespace" QualifiedIdentifier "{" NamespaceBody "}"
public readonly ref struct NamespaceDeclaration : IRule
{
    private readonly Input input;
    private readonly Int32 qualifiedIdentifierStart;
    private readonly Int32 namespaceBodyStart;
    public Int32 Length { get; }

    public NamespaceDeclaration(Input input)
    {
        this.input = input;
        var pos = 0;
        if (!input[pos..].StartsWith("namespace"))
            throw new ParseException(new ParseError());
        pos += 9;
        pos += SkipWhitespace(input[pos..]);
        qualifiedIdentifierStart = pos;
        var qualifiedIdentifier = new QualifiedIdentifier(input[pos..]);
        pos += qualifiedIdentifier.Length;
        pos += SkipWhitespace(input[pos..]);
        if (!input[pos..].StartsWith("{"))
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        namespaceBodyStart = pos;
        var namespaceBody = new NamespaceBody(input[pos..]);
        pos += namespaceBody.Length;
        pos += SkipWhitespace(input[pos..]);
        if (!input[pos..].StartsWith("}"))
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public QualifiedIdentifier QualifiedIdentifier => new(input[qualifiedIdentifierStart..]);
    public NamespaceBody NamespaceBody => new(input[namespaceBodyStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(QualifiedIdentifier);
        visitor.Visit(NamespaceBody);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// CompilationUnit
//     NamespaceDeclaration
//     TypeDeclaration
//     FunctionDeclaration
public readonly ref struct CompilationUnit : IRule
{
    private readonly Input input;
    private readonly Byte index;
    public Int32 Length { get; }

    public CompilationUnit(Input input)
    {
        this.input = input;
        if (input.StartsWith("namespace"))
        {
            var namespaceDeclaration = new NamespaceDeclaration(input);
            index = 1;
            Length = namespaceDeclaration.Length;
        }
        else if (input.StartsWith("type"))
        {
            var typeDeclaration = new TypeDeclaration(input);
            index = 2;
            Length = typeDeclaration.Length;
        }
        else if (input.StartsWith("function"))
        {
            var functionDeclaration = new FunctionDeclaration(input);
            index = 3;
            Length = functionDeclaration.Length;
        }
        else
        {
            index = Byte.MaxValue;
            Length = 0;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : GeneratedParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new NamespaceDeclaration(input)); break;
            case 2: visitor.Visit(new TypeDeclaration(input)); break;
            case 3: visitor.Visit(new FunctionDeclaration(input)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new NamespaceDeclaration(input)); break;
            case 2: visitor.Visit(new TypeDeclaration(input)); break;
            case 3: visitor.Visit(new FunctionDeclaration(input)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in NamespaceDeclaration namespaceDeclaration);
        void Visit(in TypeDeclaration typeDeclaration);
        void Visit(in FunctionDeclaration functionDeclaration);
    }
}

// BinaryOperator
//     "&&"
//     "||"
//     "=="
//     "!="
//     "<"
//     "<="
//     ">"
//     ">="
//     "+"
//     "-"
//     "*"
//     "/"
//     "%"
public readonly ref struct BinaryOperator : IRule
{
    private readonly Input input;
    public Int32 Length { get; }

    public BinaryOperator(Input input)
    {
        this.input = input;
        if (input.Length >= 2 && input[..2] is "&&" or "||" or "==" or "!=" or "<=" or ">=")
        {
            Length = 2;
            return;
        }
        if (input.Length >= 1 && input[..1] is "<" or ">" or "+" or "-" or "*" or "/" or "%")
        {
            Length = 1;
            return;
        }
        throw new ParseException(new ParseError());
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }
}

// General-purpose tree printer
public struct TreePrinter(Int32 depth = 0) : IVisitor
{
    public void Visit<T>(T rule) where T : IRule, allows ref struct
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

    public void Visit<T>(T rule) where T : IRule, allows ref struct
    {
        HasChildren = true;
    }
}

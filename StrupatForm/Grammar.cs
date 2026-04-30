using Input = System.ReadOnlySpan<System.Char>;

namespace ExampleOfGeneratedParser;

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

// Grammar -> CompilationUnit
public readonly struct Grammar(Range range)
{
    public Input Text(Input input) => input[range];

    public struct Visitor : IVisitor
    {
        public void Visit(in ParseError parseError) => throw new ParseException(parseError);
        public void Visit(in CompilationUnit compilationUnit) { }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in CompilationUnit compilationUnit);
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

    public CompilationUnit(Input input)
    {
        this.input = input;
        if (input.StartsWith("namespace"))
        {
            var ns = new NamespaceDeclaration(input);
            index = 1;
            Length = ns.Length;
        }
        else if (input.StartsWith("type"))
        {
            var td = new TypeDeclaration(input);
            index = 2;
            Length = td.Length;
        }
        else if (input.StartsWith("function"))
        {
            var fd = new FunctionDeclaration(input);
            index = 3;
            Length = fd.Length;
        }
        else
        {
            index = Byte.MaxValue;
        }
    }

    public Int32 Length { get; }
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct
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
    private readonly Int32 nameStart;
    private readonly Int32 bodyStart;
    public Int32 Length { get; }

    public NamespaceDeclaration(Input input)
    {
        this.input = input;
        var pos = 0;
        if (!input[pos..].StartsWith("namespace"))
            throw new ParseException(new ParseError());
        pos += 9;
        pos += SkipWhitespace(input[pos..]);
        nameStart = pos;
        var name = new QualifiedIdentifier(input[pos..]);
        pos += name.Length;
        pos += SkipWhitespace(input[pos..]);
        if (input[pos] != '{')
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        bodyStart = pos;
        var body = new NamespaceBody(input[pos..]);
        pos += body.Length;
        pos += SkipWhitespace(input[pos..]);
        if (input[pos] != '}')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public QualifiedIdentifier Name => new(input[nameStart..]);
    public NamespaceBody Body => new(input[bodyStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Name);
        visitor.Visit(Body);
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
            var ns = new NamespaceDeclaration(input);
            index = 1;
            Length = ns.Length;
        }
        else if (input.StartsWith("type"))
        {
            var td = new TypeDeclaration(input);
            index = 2;
            Length = td.Length;
        }
        else if (input.StartsWith("function"))
        {
            var fd = new FunctionDeclaration(input);
            index = 3;
            Length = fd.Length;
        }
        else
        {
            index = Byte.MaxValue;
            Length = 0;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct
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

// TypeDeclaration -> "type" Identifier "{" TypeBody "}"
public readonly ref struct TypeDeclaration : IRule
{
    private readonly Input input;
    private readonly Int32 nameStart;
    private readonly Int32 bodyStart;
    public Int32 Length { get; }

    public TypeDeclaration(Input input)
    {
        this.input = input;
        var pos = 0;
        if (!input[pos..].StartsWith("type"))
            throw new ParseException(new ParseError());
        pos += 4;
        pos += SkipWhitespace(input[pos..]);
        nameStart = pos;
        var name = new Identifier(input[pos..]);
        pos += name.Length;
        pos += SkipWhitespace(input[pos..]);
        if (input[pos] != '{')
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        bodyStart = pos;
        var body = new TypeBody(input[pos..]);
        pos += body.Length;
        pos += SkipWhitespace(input[pos..]);
        if (input[pos] != '}')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Identifier Name => new(input[nameStart..]);
    public TypeBody Body => new(input[bodyStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Name);
        visitor.Visit(Body);
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
            var td = new TypeDeclaration(input);
            index = 1;
            Length = td.Length;
        }
        else if (input.StartsWith("function"))
        {
            var fd = new FunctionDeclaration(input);
            index = 2;
            Length = fd.Length;
        }
        else
        {
            index = Byte.MaxValue;
            Length = 0;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct
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

// FunctionDeclaration -> "function" Identifier "{" FunctionBody "}"
public readonly ref struct FunctionDeclaration : IRule
{
    private readonly Input input;
    private readonly Int32 nameStart;
    private readonly Int32 bodyStart;
    public Int32 Length { get; }

    public FunctionDeclaration(Input input)
    {
        this.input = input;
        var pos = 0;
        if (!input[pos..].StartsWith("function"))
            throw new ParseException(new ParseError());
        pos += 8;
        pos += SkipWhitespace(input[pos..]);
        nameStart = pos;
        var name = new Identifier(input[pos..]);
        pos += name.Length;
        pos += SkipWhitespace(input[pos..]);
        if (input[pos] != '{')
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        bodyStart = pos;
        var body = new FunctionBody(input[pos..]);
        pos += body.Length;
        pos += SkipWhitespace(input[pos..]);
        if (input[pos] != '}')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Identifier Name => new(input[nameStart..]);
    public FunctionBody Body => new(input[bodyStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Name);
        visitor.Visit(Body);
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
//     Statement       (single statement, no semicolon)
//     BodyElement+    (one or more statements each followed by ';')
public readonly ref struct FunctionBody : IRule
{
    private readonly Input input;
    private readonly Byte index;
    public Int32 Length { get; }

    public FunctionBody(Input input)
    {
        this.input = input;
        var pos = 0;
        try
        {
            var first = new BodyElement(input);
            pos += first.Length;
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
            var stmt = new Statement(input);
            index = 1;
            Length = stmt.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct
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

// BodyElement -> Statement ';'
public readonly ref struct BodyElement : IRule
{
    private readonly Input input;
    public Int32 Length { get; }

    public BodyElement(Input input)
    {
        this.input = input;
        var stmt = new Statement(input);
        var pos = stmt.Length;
        pos += SkipWhitespace(input[pos..]);
        if (pos >= input.Length || input[pos] != ';')
            throw new ParseException(new ParseError());
        pos += 1;
        Length = pos;
    }

    public Statement Statement => new(input);
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
        if (input.StartsWith("return"))
        {
            var rs = new ReturnStatement(input);
            index = 4;
            Length = rs.Length;
        }
        else if (input.StartsWith("var"))
        {
            var vds = new VariableDeclarationStatement(input);
            index = 2;
            Length = vds.Length;
        }
        else
        {
            try
            {
                var a = new AssignmentStatement(input);
                index = 3;
                Length = a.Length;
            }
            catch (ParseException)
            {
                var es = new ExpressionStatement(input);
                index = 1;
                Length = es.Length;
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct
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

// ExpressionStatement -> Expression
public readonly ref struct ExpressionStatement : IRule
{
    private readonly Input input;
    public Int32 Length { get; }

    public ExpressionStatement(Input input)
    {
        this.input = input;
        var expr = new Expression(input);
        Length = expr.Length;
    }

    public Expression Expression => new(input);
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
    private readonly Int32 nameStart;
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
        nameStart = pos;
        var name = new Identifier(input[pos..]);
        pos += name.Length;
        pos += SkipWhitespace(input[pos..]);
        if (input[pos] != '=')
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        expressionStart = pos;
        var expr = new Expression(input[pos..]);
        pos += expr.Length;
        Length = pos;
    }

    public Identifier Name => new(input[nameStart..]);
    public Expression Value => new(input[expressionStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Name);
        visitor.Visit(Value);
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
    private readonly Int32 expressionStart;
    public Int32 Length { get; }

    public AssignmentStatement(Input input)
    {
        this.input = input;
        var pos = 0;
        var name = new Identifier(input[pos..]);
        pos += name.Length;
        pos += SkipWhitespace(input[pos..]);
        if (input[pos] != '=')
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        expressionStart = pos;
        var expr = new Expression(input[pos..]);
        pos += expr.Length;
        Length = pos;
    }

    public Identifier Target => new(input);
    public Expression Value => new(input[expressionStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Target);
        visitor.Visit(Value);
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
        var expr = new Expression(input[pos..]);
        pos += expr.Length;
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

// Expression
//     PrimaryExpression
//     UnaryExpression
//     BinaryExpression
public readonly ref struct Expression : IRule
{
    private readonly Input input;
    private readonly Byte index;
    private readonly Int32 leftLength;
    public Int32 Length { get; }

    public Expression(Input input)
    {
        this.input = input;
        if (input.Length > 0 && input[0] == '!')
        {
            var ue = new UnaryExpression(input);
            index = 2;
            Length = ue.Length;
            leftLength = 0;
        }
        else
        {
            var pe = new PrimaryExpression(input);
            var pos = pe.Length;
            var ws = SkipWhitespace(input[pos..]);
            if (HasBinaryOperator(input[(pos + ws)..]))
            {
                leftLength = pe.Length;
                var be = new BinaryExpression(input, leftLength);
                index = 3;
                Length = be.Length;
            }
            else
            {
                index = 1;
                Length = pe.Length;
                leftLength = 0;
            }
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new PrimaryExpression(input)); break;
            case 2: visitor.Visit(new UnaryExpression(input)); break;
            case 3: visitor.Visit(new BinaryExpression(input, leftLength)); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new PrimaryExpression(input)); break;
            case 2: visitor.Visit(new UnaryExpression(input)); break;
            case 3: visitor.Visit(new BinaryExpression(input, leftLength)); break;
            case Byte.MaxValue: visitor.Visit(new ParseError()); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in PrimaryExpression primaryExpression);
        void Visit(in UnaryExpression unaryExpression);
        void Visit(in BinaryExpression binaryExpression);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }

    private static Boolean HasBinaryOperator(Input input)
    {
        if (input.Length >= 2)
        {
            Input twoChar = input[..2];
            if (twoChar is "&&" or "||" or "==" or "!=" or "<=" or ">=")
                return true;
        }
        if (input.Length >= 1)
        {
            Input oneChar = input[..1];
            if (oneChar is "<" or ">" or "+" or "-" or "*" or "/" or "%")
                return true;
        }
        return false;
    }
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
        if (input.Length > 0 && (input[0] == '"' || Char.IsAsciiDigit(input[0])))
        {
            var lit = new Literal(input);
            index = 2;
            Length = lit.Length;
        }
        else
        {
            var id = new Identifier(input);
            index = 1;
            Length = id.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct
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

// UnaryExpression -> UnaryOperator Expression
public readonly ref struct UnaryExpression : IRule
{
    private readonly Input input;
    private readonly Int32 expressionStart;
    public Int32 Length { get; }

    public UnaryExpression(Input input)
    {
        this.input = input;
        var pos = 0;
        if (input[pos] != '!')
            throw new ParseException(new ParseError());
        pos += 1;
        pos += SkipWhitespace(input[pos..]);
        expressionStart = pos;
        var expr = new Expression(input[pos..]);
        pos += expr.Length;
        Length = pos;
    }

    public UnaryOperator Operator => new(input);
    public Expression Operand => new(input[expressionStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Operator);
        visitor.Visit(Operand);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// BinaryExpression -> Expression BinaryOperator Expression
public readonly ref struct BinaryExpression : IRule
{
    private readonly Input input;
    private readonly Int32 leftLength;
    private readonly Int32 operatorStart;
    private readonly Int32 operatorLength;
    private readonly Int32 rightStart;
    public Int32 Length { get; }

    public BinaryExpression(Input input, Int32 leftLength)
    {
        this.input = input;
        this.leftLength = leftLength;
        var pos = leftLength;
        pos += SkipWhitespace(input[pos..]);
        operatorStart = pos;
        operatorLength = 0;
        if (pos + 1 < input.Length)
        {
            Input twoChar = input[pos..(pos + 2)];
            if (twoChar is "&&" or "||" or "==" or "!=" or "<=" or ">=")
                operatorLength = 2;
        }
        if (operatorLength == 0 && pos < input.Length)
        {
            Input oneChar = input[pos..(pos + 1)];
            if (oneChar is "<" or ">" or "+" or "-" or "*" or "/" or "%")
                operatorLength = 1;
        }
        if (operatorLength == 0)
            throw new ParseException(new ParseError());
        pos += operatorLength;
        pos += SkipWhitespace(input[pos..]);
        rightStart = pos;
        var right = new Expression(input[pos..]);
        pos += right.Length;
        Length = pos;
    }

    public Expression Left => new(input[..leftLength]);
    public BinaryOperator Operator => new(operatorStart..(operatorStart + operatorLength));
    public Expression Right => new(input[rightStart..]);
    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct
    {
        visitor.Visit(Left);
        visitor.Visit(Right);
    }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
    }
}

// UnaryOperator -> "!"
public readonly ref struct UnaryOperator : IRule
{
    public UnaryOperator(Input input) => Text = input[..1];

    public Input Text { get; }

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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

// BinaryOperator -> "&&" | "||" | "==" | "!=" | "<" | "<=" | ">" | ">=" | "+" | "-" | "*" | "/" | "%"
public readonly struct BinaryOperator(Range range)
{
    public Range Range => range;
    public Input Text(Input input) => input[range];

    public static void Parse<T>(Input input, T visitor) where T : IVisitor
    {
        switch (input)
        {
            case "&&" or "||" or "==" or "!=" or "<=" or ">=": visitor.Visit(new BinaryOperator(..2)); break;
            case "<" or ">" or "+" or "-" or "*" or "/" or "%": visitor.Visit(new BinaryOperator(..1)); break;
            default: visitor.Visit(new ParseError()); break;
        }
    }

    public interface IVisitor
    {
        void Visit(in ParseError parseError);
        void Visit(in BinaryOperator binaryOperator);
    }
}

// Literal
//     '"' [^\"] '"'
//     [0-9] [0-9]*
//     '0' [xX] [0-9a-fA-F]+
public readonly ref struct Literal : IRule
{
    private readonly Input input;
    public Int32 Length { get; }
    private readonly Byte index;

    public Literal(Input input)
    {
        this.input = input;
        if (input.Length > 0 && input[0] == '"')
        {
            var end = input[1..].IndexOf('"');
            if (end < 0)
                throw new ParseException(new ParseError());
            Length = end + 2;
            index = 1;
        }
        else if (input.Length > 1 && input[0] == '0' && (input[1] == 'x' || input[1] == 'X'))
        {
            var i = 2;
            while (i < input.Length && IsHexDigit(input[i]))
                i++;
            if (i == 2)
                throw new ParseException(new ParseError());
            Length = i;
            index = 3;
        }
        else if (input.Length > 0 && Char.IsAsciiDigit(input[0]))
        {
            var i = 1;
            while (i < input.Length && Char.IsAsciiDigit(input[i]))
                i++;
            Length = i;
            index = 2;
        }
        else
        {
            throw new ParseException(new ParseError());
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new StringLiteral(input[..Length])); break;
            case 2: visitor.Visit(new DecimalLiteral(input[..Length])); break;
            case 3: visitor.Visit(new HexLiteral(input[..Length])); break;
        }
    }

    public void Visit<T>(T visitor) where T : IVisitor
    {
        switch (index)
        {
            case 0: throw new UninitializedInstanceException();
            case 1: visitor.Visit(new StringLiteral(input[..Length])); break;
            case 2: visitor.Visit(new DecimalLiteral(input[..Length])); break;
            case 3: visitor.Visit(new HexLiteral(input[..Length])); break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public interface IVisitor
    {
        void Visit(in StringLiteral stringLiteral);
        void Visit(in DecimalLiteral decimalLiteral);
        void Visit(in HexLiteral hexLiteral);
    }

    private static Boolean IsHexDigit(Char c) =>
        Char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}

public readonly ref struct StringLiteral : IRule
{
    public StringLiteral(Input input) => Text = input;
    public Input Text { get; }
    public Input Value => Text[1..^1];
    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }
}

public readonly ref struct DecimalLiteral : IRule
{
    public DecimalLiteral(Input input) => Text = input;
    public Input Text { get; }
    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }
}

public readonly ref struct HexLiteral : IRule
{
    public HexLiteral(Input input) => Text = input;
    public Input Text { get; }
    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }
}

// QualifiedIdentifier -> Identifier ('.' Identifier)*
public readonly ref struct QualifiedIdentifier : IRule
{
    private readonly Input input;
    public Int32 Length { get; }

    public QualifiedIdentifier(Input input)
    {
        this.input = input;
        var ident = new Identifier(input);
        var pos = ident.Length;
        while (pos < input.Length && input[pos] == '.')
        {
            pos += 1;
            var next = new Identifier(input[pos..]);
            pos += next.Length;
        }
        Length = pos;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }
}

// Identifier -> \w [\w\d]+
public readonly ref struct Identifier : IRule
{
    private readonly Input input;
    public Int32 Length { get; }

    public Identifier(Input input)
    {
        this.input = input;
        if (input.Length == 0 || !IsWordChar(input[0]))
            throw new ParseException(new ParseError());
        var i = 1;
        while (i < input.Length && IsWordOrDigitChar(input[i]))
            i++;
        Length = i;
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }

    private static Boolean IsWordChar(Char c) =>
        Char.IsLetter(c) || c == '_';

    private static Boolean IsWordOrDigitChar(Char c) =>
        Char.IsLetterOrDigit(c) || c == '_';
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

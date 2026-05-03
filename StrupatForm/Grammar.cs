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
    void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct;
}

public interface IVisitor
{
    void Visit<T>(in T rule) where T : IRule, allows ref struct;
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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }
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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

    private static Int32 SkipWhitespace(Input input)
    {
        var i = 0;
        while (i < input.Length && Char.IsWhiteSpace(input[i]))
            i++;
        return i;
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
        while (true)
        {
            var savedPos = pos;
            try
            {
                pos += SkipWhitespace(input[pos..]);
                var binOp = new BinaryOperator(input[pos..]);
                pos += binOp.Length;
                pos += SkipWhitespace(input[pos..]);
                var nextAtom = new Atom(input[pos..]);
                pos += nextAtom.Length;
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
            var binOp = new BinaryOperator(input[pos..]);
            visitor.Visit(binOp);
            pos += binOp.Length;
            pos += SkipWhitespace(input[pos..]);
            var nextAtom = new Atom(input[pos..]);
            visitor.Visit(nextAtom);
            pos += nextAtom.Length;
        }
    }


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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
        if (input.Length > 0 && input[0] == '!')
        {
            var ue = new UnaryExpression(input);
            index = 2;
            Length = ue.Length;
        }
        else
        {
            var pe = new PrimaryExpression(input);
            index = 1;
            Length = pe.Length;
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct
    {
        switch (index)
        {
            case 1: visitor.Visit(new PrimaryExpression(input)); break;
            case 2: visitor.Visit(new UnaryExpression(input)); break;
        }
    }


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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


    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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
    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

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
    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }
}

// Literal
//     '"' [^\"] '"'
//     [0-9] [0-9]*
//     '0' [xX] [0-9a-fA-F]+
public readonly ref struct Literal : IRule
{
    private readonly Input input;
    public Int32 Length { get; }

    public Literal(Input input)
    {
        this.input = input;
        if (input.Length > 0 && input[0] == '"')
        {
            var end = input[1..].IndexOf('"');
            if (end < 0)
                throw new ParseException(new ParseError());
            Length = end + 2;
        }
        else if (input.Length > 1 && input[0] == '0' && (input[1] == 'x' || input[1] == 'X'))
        {
            var i = 2;
            while (i < input.Length && IsHexDigit(input[i]))
                i++;
            if (i == 2)
                throw new ParseException(new ParseError());
            Length = i;
        }
        else if (input.Length > 0 && Char.IsAsciiDigit(input[0]))
        {
            var i = 1;
            while (i < input.Length && Char.IsAsciiDigit(input[i]))
                i++;
            Length = i;
        }
        else
        {
            throw new ParseException(new ParseError());
        }
    }

    public Input Text => input[..Length];

    public void VisitChildren<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }
    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

    private static Boolean IsHexDigit(Char c) =>
        Char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
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
    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }
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
    public void VisitParent<T>(ref T visitor) where T : ExampleOfGeneratedParser.IVisitor, allows ref struct { }

    private static Boolean IsWordChar(Char c) =>
        Char.IsLetter(c) || c == '_';

    private static Boolean IsWordOrDigitChar(Char c) =>
        Char.IsLetterOrDigit(c) || c == '_';
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

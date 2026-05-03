using System.Text;
using CsvParser;
using AwesomeAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Csv.Tests;

public class CsvParserTests
{
    public CsvParserTests(ITestOutputHelper output)
    {
        Console.SetOut(new TestOutputHelperTextWriter(output));
    }

    sealed class TestOutputHelperTextWriter(ITestOutputHelper output) : TextWriter
    {
        readonly StringBuilder buffer = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(Char value)
        {
            if (value == '\n')
                Flush();
            else
                buffer.Append(value);
        }

        public override void Write(String? value)
        {
            if (value == null) return;
            foreach (var c in value)
                Write(c);
        }

        public override void WriteLine(String? value)
        {
            Write(value);
            Flush();
        }

        public override void Flush()
        {
            if (buffer.Length > 0)
            {
                output.WriteLine(buffer.ToString());
                buffer.Clear();
            }
        }
    }

    [Fact]
    public void ParseSimpleField()
    {
        var field = new SimpleField("hello,world");
        field.Length.Should().Be(5);
        new String(field.Text).Should().Be("hello");
    }

    [Fact]
    public void ParseSimpleFieldStopsAtNewline()
    {
        var field = new SimpleField("hello\nworld");
        field.Length.Should().Be(5);
    }

    [Fact]
    public void ParseQuotedField()
    {
        var field = new QuotedField("\"hello\"");
        field.Length.Should().Be(7);
    }

    [Fact]
    public void ParseQuotedFieldWithEscapedQuote()
    {
        var field = new QuotedField("\"say \"\"hi\"\"\"");
        field.Length.Should().Be(12);
    }

    [Fact]
    public void ParseFieldSelectsSimple()
    {
        var field = new Field("hello,more");
        field.Length.Should().Be(5);
        new String(field.Text).Should().Be("hello");
    }

    [Fact]
    public void ParseFieldSelectsQuoted()
    {
        var field = new Field("\"hello\",more");
        field.Length.Should().Be(7);
        new String(field.Text).Should().Be("\"hello\"");
    }

    [Fact]
    public void ParseRow()
    {
        var row = new Row("a,b,c\n");
        row.Length.Should().Be(6);
    }

    [Fact]
    public void ParseRowVisitFields()
    {
        var row = new Row("a,b,c\n");

        var fields = new List<String>();
        var collector = new FieldCollector(fields);
        row.VisitChildren(ref collector);

        fields.Should().HaveCount(3);
        fields[0].Should().Be("a");
        fields[1].Should().Be("b");
        fields[2].Should().Be("c");
    }

    [Fact]
    public void ParseFile()
    {
        var file = new CsvParser.File("a,b\nc,d\n");
        file.Length.Should().Be(8);
    }

    [Fact]
    public void TreePrinterWorks()
    {
        var file = new CsvParser.File("a,b\n\"c\",d\n");
        var printer = new TreePrinter();
        printer.Visit(file);
    }

    struct FieldCollector(List<String> fields) : IVisitor
    {
        public void Visit<T>(in T rule) where T : IRule, allows ref struct
        {
            if (typeof(T) == typeof(SimpleField) || typeof(T) == typeof(QuotedField))
                fields.Add(new String(rule.Text));
            else
                rule.VisitChildren(ref this);
        }
    }
}

using AwesomeAssertions;
using StrupatForm;
using Xunit;

namespace Grammar.Tests;

public class CodeEmitterTests
{
    [Fact]
    public void EmitSimpleRule()
    {
        var grammar = SfGrammarBuilder.Parse("Digit -> [0-9]");
        var code = CodeEmitter.Emit(grammar, "TestParser");
        code.Should().Contain("namespace TestParser");
        code.Should().Contain("ref struct Digit");
    }

    [Fact]
    public void EmitAlternativeRule()
    {
        var grammar = SfGrammarBuilder.Parse("Choice\n\t'a'\n\t'b'\n\t'c'");
        var code = CodeEmitter.Emit(grammar, "TestParser");
        code.Should().Contain("ref struct Choice");
    }

    [Fact]
    public void EmitSequenceRule()
    {
        var grammar = SfGrammarBuilder.Parse("Pair -> 'a' 'b'");
        var code = CodeEmitter.Emit(grammar, "TestParser");
        code.Should().Contain("ref struct Pair");
    }

    [Fact]
    public void EmitRuleWithQuantifier()
    {
        var grammar = SfGrammarBuilder.Parse("Digits -> [0-9]+");
        var code = CodeEmitter.Emit(grammar, "TestParser");
        code.Should().Contain("ref struct Digits");
    }

    [Fact]
    public void EmitMultipleRules()
    {
        var input = "File -> Row+\n\nRow -> Field '\\n'\n\nField -> [^\\n]+";
        var grammar = SfGrammarBuilder.Parse(input);
        var code = CodeEmitter.Emit(grammar, "TestParser");
        code.Should().Contain("ref struct File");
        code.Should().Contain("ref struct Row");
        code.Should().Contain("ref struct Field");
    }

    [Fact]
    public void EmitCsvGrammar()
    {
        var text = System.IO.File.ReadAllText("TestData/CSV.sf");
        var grammar = SfGrammarBuilder.Parse(text);
        var code = CodeEmitter.Emit(grammar, "CsvParser");
        code.Should().Contain("namespace CsvParser");
        code.Should().Contain("ref struct File");
        code.Should().Contain("ref struct Row");
        code.Should().Contain("ref struct Field");
    }
}

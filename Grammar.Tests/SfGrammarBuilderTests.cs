using AwesomeAssertions;
using StrupatForm;
using Xunit;

namespace Grammar.Tests;

public class SfGrammarBuilderTests
{
    [Fact]
    public void ParseInlineRule()
    {
        var grammar = SfGrammarBuilder.Parse("Name -> [\\w] [\\w\\d]*");
        grammar.Rules.Should().HaveCount(1);
        var rule = grammar.Rules.First();
        rule.Name.Should().Be("Name");
        rule.Alternatives.Should().HaveCount(1);
    }

    [Fact]
    public void ParseIndentedAlternatives()
    {
        var grammar = SfGrammarBuilder.Parse("Quantifier\n\t'?'\n\t'*'\n\t'+'");
        var rule = grammar.Rules.First();
        rule.Name.Should().Be("Quantifier");
        rule.Alternatives.Should().HaveCount(3);
    }

    [Fact]
    public void ParseRuleRef()
    {
        var grammar = SfGrammarBuilder.Parse("Rule -> Name Alternatives\n\nName -> [\\w]+\n\nAlternatives -> Name");
        grammar.Rules.Should().HaveCount(3);
    }

    [Fact]
    public void ParseStringLiteralInAlternative()
    {
        var grammar = SfGrammarBuilder.Parse("Greeting -> \"hello\"");
        var rule = grammar.Rules.First();
        var items = rule.Alternatives[0].Items;
        items.Should().HaveCount(1);
        items[0].Should().BeOfType<Literal<String>>();
        ((Literal<String>)items[0]).Value.Should().Be("hello");
    }

    [Fact]
    public void ParseCharLiteralInAlternative()
    {
        var grammar = SfGrammarBuilder.Parse("Comma -> ','");
        var rule = grammar.Rules.First();
        var items = rule.Alternatives[0].Items;
        items.Should().HaveCount(1);
        items[0].Should().BeOfType<Literal<Char>>();
        ((Literal<Char>)items[0]).Value.Should().Be(',');
    }

    [Fact]
    public void ParseEscapedCharLiteral()
    {
        var grammar = SfGrammarBuilder.Parse("Newline -> '\\n'");
        var rule = grammar.Rules.First();
        var items = rule.Alternatives[0].Items;
        items[0].Should().BeOfType<Literal<Char>>();
        ((Literal<Char>)items[0]).Value.Should().Be('\n');
    }

    [Fact]
    public void ParseCharClassInAlternative()
    {
        var grammar = SfGrammarBuilder.Parse("Digit -> [0-9]");
        var rule = grammar.Rules.First();
        var items = rule.Alternatives[0].Items;
        items.Should().HaveCount(1);
        items[0].Should().BeOfType<Class>();
    }

    [Fact]
    public void ParseNegatedCharClass()
    {
        var grammar = SfGrammarBuilder.Parse("NonQuote -> [^\\\"]");
        var rule = grammar.Rules.First();
        var cls = (Class)rule.Alternatives[0].Items[0];
        cls.Negated.Should().BeTrue();
    }

    [Fact]
    public void ParseQuantifierZeroOrMore()
    {
        var grammar = SfGrammarBuilder.Parse("Chars -> [a-z]*");
        var cls = (Class)grammar.Rules.First().Alternatives[0].Items[0];
        cls.Quantifier.Min.Should().Be(0u);
        cls.Quantifier.Max.Should().BeNull();
    }

    [Fact]
    public void ParseQuantifierOneOrMore()
    {
        var grammar = SfGrammarBuilder.Parse("Chars -> [a-z]+");
        var cls = (Class)grammar.Rules.First().Alternatives[0].Items[0];
        cls.Quantifier.Min.Should().Be(1u);
        cls.Quantifier.Max.Should().BeNull();
    }

    [Fact]
    public void ParseQuantifierOptional()
    {
        var grammar = SfGrammarBuilder.Parse("MaybeChar -> [a-z]?");
        var cls = (Class)grammar.Rules.First().Alternatives[0].Items[0];
        cls.Quantifier.Min.Should().Be(0u);
        cls.Quantifier.Max.Should().Be(1u);
    }

    [Fact]
    public void ParseGroupInAlternative()
    {
        var grammar = SfGrammarBuilder.Parse("List -> Item (',' Item)*\n\nItem -> [a-z]+");
        grammar.Rules.Should().HaveCount(2);
        var rule = grammar.Rules.First(r => r.Name == "List");
        rule.Alternatives[0].Items.Should().HaveCount(2);
    }

    [Fact]
    public void ParseMultipleRules()
    {
        var input = "File -> Row+\n\nRow -> Field (',' Field)* '\\n'\n\nField -> [^,\\n]+";
        var grammar = SfGrammarBuilder.Parse(input);
        grammar.Rules.Should().HaveCount(3);
    }

    [Fact]
    public void ParseSelfHostedGrammar()
    {
        var text = System.IO.File.ReadAllText("TestData/StrupatForm.sf");
        var grammar = SfGrammarBuilder.Parse(text);
        grammar.Rules.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseUnicodePropertyClassAlpha()
    {
        var grammar = SfGrammarBuilder.Parse("Letter -> [\\p{Alpha}]");
        var rule = grammar.Rules.First();
        var cls = (Class)rule.Alternatives[0].Items[0];
        cls.Ranges.Should().HaveCount(1);
    }

    [Fact]
    public void ParseUnicodePropertyClassDecimalNumber()
    {
        var grammar = SfGrammarBuilder.Parse("Digit -> [\\p{Decimal_Number}]");
        var rule = grammar.Rules.First();
        var cls = (Class)rule.Alternatives[0].Items[0];
        cls.Ranges.Should().HaveCount(1);
    }

    [Fact]
    public void ParseUnicodePropertyClassGeneralCategory()
    {
        var grammar = SfGrammarBuilder.Parse("OtherLetter -> [\\p{General_Category=Other_Letter}]");
        var rule = grammar.Rules.First();
        var cls = (Class)rule.Alternatives[0].Items[0];
        cls.Ranges.Should().HaveCount(1);
    }

    [Fact]
    public void ParseUnicodePropertyClassWithShorthand()
    {
        var grammar = SfGrammarBuilder.Parse("AlphaNum -> [\\p{Alpha}\\p{Decimal_Number}]");
        var rule = grammar.Rules.First();
        var cls = (Class)rule.Alternatives[0].Items[0];
        cls.Ranges.Should().HaveCount(2);
    }

    [Fact]
    public void ParseUnicodeEscapeU4()
    {
        var grammar = SfGrammarBuilder.Parse("A -> '\\u0041'");
        var rule = grammar.Rules.First();
        var items = rule.Alternatives[0].Items;
        items[0].Should().BeOfType<Literal<Char>>();
        ((Literal<Char>)items[0]).Value.Should().Be('A');
    }

    [Fact]
    public void ParseUnicodeEscapeU8()
    {
        var grammar = SfGrammarBuilder.Parse("A -> '\\U00000041'");
        var rule = grammar.Rules.First();
        var items = rule.Alternatives[0].Items;
        items[0].Should().BeOfType<Literal<Char>>();
        ((Literal<Char>)items[0]).Value.Should().Be('A');
    }

    [Fact]
    public void ParseUnicodeEscapeHex()
    {
        var grammar = SfGrammarBuilder.Parse("A -> '\\x{41}'");
        var rule = grammar.Rules.First();
        var items = rule.Alternatives[0].Items;
        items[0].Should().BeOfType<Literal<Char>>();
        ((Literal<Char>)items[0]).Value.Should().Be('A');
    }

    [Fact]
    public void ParseUnicodeEscapeInString()
    {
        var grammar = SfGrammarBuilder.Parse("AB -> \"\\u0041\\u0042\"");
        var rule = grammar.Rules.First();
        var items = rule.Alternatives[0].Items;
        items[0].Should().BeOfType<Literal<String>>();
        ((Literal<String>)items[0]).Value.Should().Be("AB");
    }

    [Fact]
    public void ParseQuantifierExactly()
    {
        var grammar = SfGrammarBuilder.Parse("Hex4 -> [0-9a-fA-F]{4}");
        var cls = (Class)grammar.Rules.First().Alternatives[0].Items[0];
        cls.Quantifier.Min.Should().Be(4u);
        cls.Quantifier.Max.Should().Be(4u);
    }

    [Fact]
    public void ParseQuantifierAtLeast()
    {
        var grammar = SfGrammarBuilder.Parse("Digits -> [0-9]{1,}");
        var cls = (Class)grammar.Rules.First().Alternatives[0].Items[0];
        cls.Quantifier.Min.Should().Be(1u);
        cls.Quantifier.Max.Should().BeNull();
    }

    [Fact]
    public void ParseQuantifierBetween()
    {
        var grammar = SfGrammarBuilder.Parse("Hex -> [0-9a-fA-F]{1,8}");
        var cls = (Class)grammar.Rules.First().Alternatives[0].Items[0];
        cls.Quantifier.Min.Should().Be(1u);
        cls.Quantifier.Max.Should().Be(8u);
    }
}

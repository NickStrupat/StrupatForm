using AwesomeAssertions;
using SfParser;
using Xunit;

namespace Grammar.Tests;

public class SfParserTests
{
    [Fact]
    public void ParseName()
    {
        var name = new Name("MyRule rest");
        name.Length.Should().Be(6);
        new String(name.Text).Should().Be("MyRule");
    }

    [Fact]
    public void ParseNameWithDigits()
    {
        var name = new Name("rule123 rest");
        name.Length.Should().Be(7);
    }

    [Fact]
    public void ParseNameRejectsDigitStart()
    {
        var name = new Name("123abc");
        name.Length.Should().Be(-1);
    }

    [Fact]
    public void ParseCharLiteral()
    {
        var lit = new CharLiteral("'a' rest");
        lit.Length.Should().Be(3);
        new String(lit.Text).Should().Be("'a'");
    }

    [Fact]
    public void ParseCharLiteralWithEscape()
    {
        var lit = new CharLiteral("'\\n'");
        lit.Length.Should().Be(4);
    }

    [Fact]
    public void ParseStringLiteral()
    {
        var lit = new StringLiteral("\"hello\" rest");
        lit.Length.Should().Be(7);
        new String(lit.Text).Should().Be("\"hello\"");
    }

    [Fact]
    public void ParseStringLiteralWithEscapes()
    {
        var lit = new StringLiteral("\"line\\nbreak\"");
        lit.Length.Should().Be(13);
    }

    [Fact]
    public void ParseEmptyStringLiteral()
    {
        var lit = new StringLiteral("\"\"");
        lit.Length.Should().Be(2);
    }

    [Fact]
    public void ParseCharClass()
    {
        var cls = new Class("[a-z]");
        cls.Length.Should().Be(5);
    }

    [Fact]
    public void ParseNegatedCharClass()
    {
        var cls = new Class("[^\\\\\\']");
        cls.Length.Should().Be(7);
    }

    [Fact]
    public void ParseShorthandClass()
    {
        var cls = new Class("[\\w]");
        cls.Length.Should().Be(4);
    }

    [Fact]
    public void ParseCharRange()
    {
        var range = new CharRange("a-z]");
        range.Length.Should().Be(3);
    }

    [Fact]
    public void ParseQuantifierQuestion()
    {
        var q = new Quantifier("?");
        q.Length.Should().Be(1);
    }

    [Fact]
    public void ParseQuantifierStar()
    {
        var q = new Quantifier("*");
        q.Length.Should().Be(1);
    }

    [Fact]
    public void ParseQuantifierPlus()
    {
        var q = new Quantifier("+");
        q.Length.Should().Be(1);
    }

    [Fact]
    public void ParseItem()
    {
        var item = new Item("[a-z]+");
        item.Length.Should().Be(6);
    }

    [Fact]
    public void ParseItemWithoutQuantifier()
    {
        var item = new Item("MyRule rest");
        item.Length.Should().Be(6);
    }

    [Fact]
    public void ParseAlternative()
    {
        var alt = new Alternative("'\\'' CharLiteralContent '\\''");
        alt.Length.Should().Be(28);
    }

    [Fact]
    public void ParseGroup()
    {
        var grp = new Group("(' ' Item)*");
        grp.Length.Should().Be(10);
    }

    [Fact]
    public void ParseInlineRule()
    {
        var rule = new SfParser.Rule("Name -> [\\w] [\\w\\d]*");
        rule.Length.Should().Be(20);
        new String(rule.Name.Text).Should().Be("Name");
    }

    [Fact]
    public void ParseIndentedRule()
    {
        var rule = new SfParser.Rule("Quantifier\n\t'?'\n\t'*'\n\t'+'");
        new String(rule.Name.Text).Should().Be("Quantifier");
    }

    [Fact]
    public void ParseMultipleRulesInGrammar()
    {
        var grammar = new SfParser.Grammar("RuleA -> 'a'\n\nRuleB -> 'b'");
        grammar.Length.Should().Be(26);
    }
}

using System;
using AwesomeAssertions;
using StrupatForm;
using Xunit;

namespace Tests;

public class UnescapeTests
{
	[Theory]
	[InlineData("x\\n\\r\\t", "x\n\r\t")]
	[InlineData("\\\"", "\"")]
	[InlineData("\\'", "\'")]
	public void Single(String input, String expected)
	{
		var unescaped = SfGrammarBuilder.Unescape(input);
		unescaped.Should().BeEquivalentTo(expected);
	}

	[Theory]
	[InlineData("\\u0041", "A")]
	[InlineData("A\\u0042C", "ABC")]
	public void Unicode4Escape(String input, String expected)
	{
		var what = SfGrammarBuilder.Unescape(input);
		what.Should().BeEquivalentTo(expected);
	}

	[Fact]
	public void Unicode8Escape()
	{
		var what = SfGrammarBuilder.Unescape("\\U00000041");
		what.Should().BeEquivalentTo("A");
	}

	[Fact]
	public void HexEscape()
	{
		var what = SfGrammarBuilder.Unescape("\\x{41}");
		what.Should().BeEquivalentTo("A");
	}
}

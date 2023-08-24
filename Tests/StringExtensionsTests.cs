using System;
using FluentAssertions;
using StrupatForm;
using Xunit;

namespace Tests;

public class StringExtensionsTests
{
	[Theory]
	[InlineData("x\\n\\r\\t", "x\n\r\t")]
	[InlineData("\\\"", "\"")]
	[InlineData("\\'", "\'")]
	public void Single(String input, String expected)
	{
		var unescaped = input.ToUnescaped();
		unescaped.Should().BeEquivalentTo(expected);
	}

	[Theory]
	[InlineData("\\u0041", "A")]
	[InlineData(@"A\u0042C", "ABC")]
	public void Unicode4Escape(String input, String expected)
	{
		String text = input;
		var what = text.ToUnescaped();
		what.Should().BeEquivalentTo(expected);
	}

	[Fact]
	public void Unicode8Escape()
	{
		String text = "\\U00000041";
		var what = text.ToUnescaped();
		what.Should().BeEquivalentTo("A");
	}

	[Fact]
	public void HexEscape()
	{
		String text = "\\x{41}";
		var what = text.ToUnescaped();
		what.Should().BeEquivalentTo("A");
	}
}

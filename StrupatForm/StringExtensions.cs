using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StrupatForm;

public static class StringExtensions
{
	private static readonly Regex EscapeRegex =
		new(@"\\([0\\tnr""']|u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|x{[0-9a-fA-F]{1,8}})", RegexOptions.Compiled);

	public static String ToUnescaped(this String text)
	{
		return EscapeRegex.Replace(text, MatchEvaluator);

		static String MatchEvaluator(Match m) =>
			m.Groups[1].Value switch
			{
				"0" => "\0",
				"\\" => "\\",
				"t" => "\t",
				"n" => "\n",
				"r" => "\r",
				"\"" => "\"",
				"'" => "'",
				{ } x when x[0] is 'u' or 'U' => HexToString(x, 1..),
				{ } x when x[0] is 'x' => HexToString(x, 2..x.IndexOf('}')),
				_ => throw new("ruh roh"),
			};

		static String HexToString(String s, System.Range r) =>
			new Rune(UInt32.Parse(s.AsSpan()[r], NumberStyles.AllowHexSpecifier)).ToString();
	}
}

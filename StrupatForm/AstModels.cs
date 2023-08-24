using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace StrupatForm;

public sealed class Grammar
{
	public HashSet<Rule> Rules { get; } = new(EqCmp<Rule>.Create(x => x.Name));
}

public sealed class Rule
{
	public required String Name { get; init; }
	public List<Alternative> Alternatives { get; } = new();
	public override String ToString() => Name;
}

public sealed class Alternative : Item
{
	public List<Item> Items { get; } = new();
}

public abstract class Item
{
	public required Quantifier Quantifier { get; init; }
}

public sealed class Quantifier
{
	public required UInt32 Min { get; init; }
	public required UInt32? Max { get; init; }
	public override String ToString() => (Min, Max) switch
	{
		(0, 1) => "?",
		(0, null) => "*",
		(1, null) => "+",
		(_, null) => $">={Min}",
		(_, _) when Min == Max => $"{Min}",
		(_, _) => $"{Min}-{Max}",
	};
}

public sealed class Class : Item
{
	public required Boolean Negated { get; init; }
	public HashSet<Range> Ranges { get; } = new();
}

public abstract class Range : IEquatable<Range>
{
	public abstract Boolean Contains(Rune r);
	public abstract Boolean Equals(Range? other);
}

public sealed class CharacterRange : Range
{
	public required Rune From { get; init; }
	public required Rune To { get; init; }
	public override Boolean Contains(Rune r) => From <= r && r <= To;
	public override Boolean Equals(Range? other) => other is CharacterRange cr && (From, To) == (cr.From, cr.To);
	public override String ToString() => From == To ? $"{From}" : $"{From}-{To}";
}

public sealed class RegexCharacterRange : Range
{
	public required String Pattern { get; init; }
	public override Boolean Contains(Rune r)
	{
		Span<Char> chars = stackalloc Char[2];
		var n = r.EncodeToUtf16(chars);
		return Regex.IsMatch(chars[..n]);
	}

	public override Boolean Equals(Range? other) => other is RegexCharacterRange rcr && Pattern == rcr.Pattern;

	private Regex Regex => Cache.GetOrAdd(Pattern, x => new(x, RegexOptions.Compiled));
	private static readonly ConcurrentDictionary<String, Regex> Cache = new();
}

public abstract class Literal : Item  {}

public sealed class Literal<T> : Literal where T : notnull
{
	public required T Value { get; init; }
	public override String ToString() => $"{Value} {{{Quantifier}}}";
}

public sealed class RuleRef : Item
{
	public required String Name { get; init; }
	public required Rule Rule { get; init; }
	public override String ToString() => $"{Name} {{{Quantifier}}}";
}

using System;
using System.Text.RegularExpressions;

const String csv = """
                   1,2,3
                   4,5,6
                   7,8,9
                   """;

var csvFile = new CsvFile(csv);
foreach (var row in csvFile.Row)
{
	foreach (var field in row.Field)
		Console.Out.Write(field.Value);
	Console.WriteLine();
}

public readonly ref struct CsvFile
{
	private readonly ReadOnlySpan<Char> input;

	public CsvFile(ReadOnlySpan<Char> input)
	{
		this.input = input;
		Row = new(input);
	}

	public Int32 Length => Row.Length;

	public RowEnumerable Row { get; }
}

public readonly ref struct RowEnumerable
{
	private readonly ReadOnlySpan<Char> input;
	public RowEnumerable(ReadOnlySpan<Char> input)
	{
		this.input = input;
		foreach (var row in this)
			Length += row.Length;
	}

	public Int32 Length { get; }

	public Enumerator GetEnumerator() => new(input);

	public ref struct Enumerator
	{
		private readonly ReadOnlySpan<Char> input;
		public Enumerator(ReadOnlySpan<Char> input) => this.input = input;

		public Int32 Length { get; private set; }
		public Row Current { get; private set; }
		private ReadOnlySpan<Char> Next => input[Length..];

		public Boolean MoveNext()
		{
			if (Next.IsEmpty)
				return false;
			Current = new(Next);
			Length += Current.Length;
			return true;
		}
	}
}

public readonly ref struct Row
{
	private readonly ReadOnlySpan<Char> input;
	public Row(ReadOnlySpan<Char> input)
	{
		this.input = input;
		Field = new(input);
		Length = Field.Length;
	}

	public FieldEnumerable Field { get; }
	public Int32 Length { get; }
}

public readonly ref struct FieldEnumerable
{
	private readonly ReadOnlySpan<Char> input;
	public Int32 Length { get; }
	public FieldEnumerable(ReadOnlySpan<Char> input)
	{
		this.input = input;
		foreach (var field in this)
			Length += field.Length;
	}

	public Enumerator GetEnumerator() => new(input);

	public ref struct Enumerator
	{
		private readonly ReadOnlySpan<Char> input;
		public Enumerator(ReadOnlySpan<Char> input) => this.input = input;

		public Int32 Length { get; private set; }
		public Field Current { get; private set; }
		private ReadOnlySpan<Char> Next => input[Length..];
		public Boolean MoveNext()
		{
			if (Next.IsEmpty)
				return false;
			Current = new(Next);
			Length += Current.Length;
			return true;
		}
	}
}

public readonly ref struct Field
{
	private readonly ReadOnlySpan<Char> input;
	public ReadOnlySpan<Char> Value { get; }

	public Field(ReadOnlySpan<Char> input)
	{
		this.input = input;
		var match = Regexes.Field.Match(input);
		Value = input[..match.Length];
		Length = match.Length;
		if (input.Length > Length && input[Length] == ',')
			Length += 1;
	}

	public Int32 Length { get; }

	public ReadOnlySpan<Char> Text => input[..Length];
}

public static partial class Regexes
{
	public static Regex Field => FieldRegex();
	public static Regex Newline => NewlineRegex();

	public static ValueMatch Match(this Regex regex, ReadOnlySpan<Char> text)
	{
		foreach (var match in regex.EnumerateMatches(text))
			return match;
		throw new("No match found");
	}

	public static Boolean TryMatch(this Regex regex, ReadOnlySpan<Char> text, out ValueMatch range)
	{
		foreach (var match in regex.EnumerateMatches(text))
		{
			range = match;
			return true;
		}
		range = default;
		return false;
	}

	[GeneratedRegex("""
	                "[^"]*"|[^",]*
	                """)]
	private static partial Regex FieldRegex();

	[GeneratedRegex("""
	                ^\n
	                """)]
	private static partial Regex NewlineRegex();
}

interface IParser<T> where T : IParser<T>
{
	static abstract T Parse(ReadOnlySpan<Char> text);
	public Int32 Length { get; }
	public ReadOnlySpan<T> Children { get; }
}

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using ExampleOfGeneratedParser;
using Humanizer;
using StrupatForm;
using Range = System.Range;

var sample = """
function main {
    var x = 42;
    var name = "hello";
    x = x + 1;
    return x;
}
""";

var cu = new CompilationUnit(sample);
var treePrinter = new TreePrinter();
treePrinter.Visit(cu);
return;

// var source = """
//              One
//              Two
//              """.AsSpan();
// var g = new Grammar(source);
// foreach (var rule in g.Rules)
// {
// 	Console.Out.WriteLine(rule.Name);
// }
// return;

const String path = "grammar.sf";
var text = File.ReadAllText(path);
var stream = new CodePointCharStream(text) {name = path};
var lexer = new StrupatFormLexer(stream);
var tokens = new CommonTokenStream(lexer);
//Console.WriteLine(string.Join('\n', lexer.GetAllTokens().Where(x => !String.IsNullOrWhiteSpace(x.Text)).Select(x => $"{GetName(x),-20}{x.Text}")));
var parser = new StrupatFormParser(tokens);
var grammarCtx = parser.grammar_();
var grammar = grammarCtx.ToGrammar();
var rules = grammar.Rules.OrderBy(x => x.RefCount).ToList();
// if (ExampleOfGeneratedParser.Grammar.TryParse("asdf".AsMemory(), out var x, out var e))
// 	;
// else
// 	;
;

foreach (var rule in grammar.Rules)
{
	Console.Out.WriteLine($$"""
		public sealed class {{rule.Name}} : IParsable<{{rule.Name}}>
		{
			public static Boolean TryParse(
				ReadOnlySpan<Char> input,
				[NotNullWhen(true)] out {{rule.Name}}? parseable,
				[NotNullWhen(false)] out ParseError? errors)
			{
				parseable = default;
				errors = default;
				return false;
			}
		}
		""");
}

return;

var parserCode = new StringBuilder();
parserCode.AppendLine("using System;");
parserCode.AppendLine("using System.Text.RegularExpressions;");
parserCode.AppendLine();
var grammarName = Path.GetFileNameWithoutExtension(path).ApplyCase(LetterCasing.Title).Replace(" ", "");
parserCode.AppendLine($$"""
	public readonly ref struct {{grammarName}};
	{
		private readonly ReadOnlySpan<Char> input;
		public {{grammarName}}(ReadOnlySpan<Char> input)
		{
			this.input = input;
			Rules = new(input);
		}
		public ReadOnlySpan<Char> Text => input[Range];
		public Range Range { get; }
		public RulesEnumerable Rules { get; }
	}
	""");

Console.WriteLine(parserCode);

static string GetName(IToken token) => StrupatFormParser.DefaultVocabulary.GetSymbolicName(token.Type);

struct CompilationUnitPrinter : CompilationUnit.IVisitor
{
    public void Visit(in ParseError parseError) => Console.WriteLine("Parse error!");
    public void Visit(in NamespaceDeclaration ns) => Console.WriteLine($"NamespaceDeclaration [{ns.Length} chars]");
    public void Visit(in TypeDeclaration td) => Console.WriteLine($"TypeDeclaration [{td.Length} chars]");
    public void Visit(in FunctionDeclaration fd) => Console.WriteLine($"FunctionDeclaration [{fd.Length} chars]");
}

namespace StrupatFormGrammar
{
	public readonly ref struct Grammar
	{
		private readonly ReadOnlySpan<Char> input;
		public Range Range { get; }

		public Grammar(ReadOnlySpan<Char> input)
		{
			this.input = input;
			Rules = new(input);
			Range = Rules.Range;
		}

		public ReadOnlySpan<Char> Text => input[Range];
		public RulesEnumerable Rules { get; }
	}

	public readonly ref struct RulesEnumerable
	{
		private readonly ReadOnlySpan<Char> input;

		public RulesEnumerable(ReadOnlySpan<Char> input)
		{
			this.input = input;
			Enumerator enumerator = new(input);
			while (enumerator.MoveNext())
				Range = new(Range.Start, enumerator.Range.End);
		}

		public Range Range { get; }
		public ReadOnlySpan<Char> Text => input[Range];
		public Enumerator GetEnumerator() => new(input);

		public ref struct Enumerator
		{
			private readonly ReadOnlySpan<Char> input;
			public Enumerator(ReadOnlySpan<Char> input) => this.input = input;

			public Range Range { get; private set; }
			public ReadOnlySpan<Char> Text => input[Range];

			private Int32 consumed;
			public Rule Current { get; private set; }
			private ReadOnlySpan<Char> Next => input[consumed..];

			public Boolean MoveNext()
			{
				if (Next.IsEmpty)
					return false;
				if (!Current.Text.IsEmpty)
				{
					if (!Regexes.Newline.IsMatch(Next))
						return false;
					consumed += 1;
				}

				Current = new(Next);
				consumed += Current.Range.Length();
				Range = Current.Range;
				return true;
			}
		}
	}

	public readonly ref struct Rule
	{
		private readonly ReadOnlySpan<Char> input;
		public Range Range { get; } = new();

		public Rule(ReadOnlySpan<Char> input)
		{
			this.input = input;
			var nameRange = Regexes.Name.Match(input).ToRange();
			Name = input[nameRange];
			Range = nameRange;
			//Alternatives = new(text[nameRange.End..]);
			//Text = text[..Alternatives.Text.Length];
		}

		public ReadOnlySpan<Char> Text => input[Range];

		public ReadOnlySpan<Char> Name { get; }
		//public AlternativesEnumerable Alternatives { get; }
	}

	public readonly ref struct AlternativesEnumerable
	{
		internal readonly ReadOnlySpan<Char> Text;

		public AlternativesEnumerable(ReadOnlySpan<Char> text)
		{
			Enumerator enumerator = new(text);
			while (enumerator.MoveNext())
			{
			}

			Text = enumerator.Text;
		}

		public Enumerator GetEnumerator() => new(Text);

		public ref struct Enumerator
		{
			internal readonly ReadOnlySpan<Char> Text;
			private Int32 index;

			public Enumerator(ReadOnlySpan<Char> text)
			{
				this.Text = text;
				index = -1;
			}

			public Alternative Current => default!; //new(text.Slice(index, text.IndexOf('|', index) - index));
			public Boolean MoveNext() => false; //(index = text.IndexOf('|', index) + 1) > 0;
		}
	}

	public static partial class Regexes
	{
		public static Regex Name { get; } = NameRegex();
		public static Regex Newline { get; } = NewlineRegex();

		[GeneratedRegex("""^\w[\w\d]*""")]
		private static partial Regex NameRegex();

		[GeneratedRegex("""^\n""")]
		private static partial Regex NewlineRegex();

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

		public static Range ToRange(this ValueMatch vm) => new(vm.Index, vm.Index + vm.Length);
	}

	public static class RangeExtensions
	{
		public static Int32 Length(in this Range range) => range.End.Value - range.Start.Value;
	}

	public ref struct SourceText
	{
		public Int32 Index { get; }
		public Int32 Length { get; }
		public ReadOnlySpan<Char> Text { get; }

		public SourceText(Int32 index, Int32 length, ReadOnlySpan<Char> text)
		{
			Index = index;
			Length = length;
			Text = text;
		}

		public Match TryMatch(Char c)
		{
			if (Text[0] == c)
				return new Match();
			return default;
		}
	}

	public readonly ref struct Match
	{
		private readonly ReadOnlySpan<Char> input;
	}
}

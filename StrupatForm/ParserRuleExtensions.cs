using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using static StrupatForm.StrupatFormParser;

namespace StrupatForm;

static class ParserRuleExtensions
{
	public static TTo To<TTo>(this IParseTree o) where TTo : IParseTree => (TTo) o;

	public static TAncestor? FindAncestor<TAncestor>(this RuleContext context) where TAncestor : RuleContext
	{
		while (context.Parent is not null)
			if (context.Parent is TAncestor ancestor)
				return ancestor;
			else
				context = context.Parent;
		return null;
	}

	public static Rule_Context? GetRuleContext(this RuleRefContext context)
	{
		var grammarContext = context.FindAncestor<Grammar_Context>();
		if (grammarContext is null)
			return null;
		return grammarContext.RuleLookup.TryGetValue(context.Name().GetText(), out var rule) ? rule : null;
	}

	public static Grammar ToGrammar(this Grammar_Context context)
	{
		var grammar = new Grammar();
		foreach (var ruleCtx in context.rule_())
			if (!grammar.Rules.Add(new Rule { Name = ruleCtx.Name().GetText() }))
				throw new Exception($"Rule `{ruleCtx.Name().GetText()}` defined multiple times");
		foreach (var ruleCtx in context.rule_())
		{
			var name = ruleCtx.Name().GetText();
			var rule = grammar.Rules.Get(name);
			foreach (var alternativeCtx in ruleCtx.alternatives().alternative())
				rule.Alternatives.Add(alternativeCtx.ToAlternative(grammar));
		}

		return grammar;
	}

	public static Rule ToRule(this Rule_Context context, Grammar grammar)
	{
		var rule = new Rule {Name = context.Name().GetText()};
		foreach (var alternative in context.alternatives().alternative())
			rule.Alternatives.Add(alternative.ToAlternative(grammar));
		return rule;
	}

	public static Alternative ToAlternative(this AlternativeContext context, Grammar grammar)
	{
		var alternative = new Alternative {Quantifier = new() {Min = 1, Max = 1}};
		foreach (var item in context.item())
			alternative.Items.Add(item.ToItem(grammar));
		return alternative;
	}

	public static Item ToItem(this ItemContext context, Grammar grammar)
	{
		var quantifier = context.quantifier().ToQuantifier();
		var childContext = context.GetRuleContext<ParserRuleContextEx>(0);
		return childContext switch
		{
			RuleRefContext ruleRefContext => ruleRefContext.ToRuleRef(grammar, quantifier),
			LiteralContext literalContext => literalContext.ToLiteral(quantifier),
			ClassContext classContext => classContext.ToClass(quantifier),
			AlternativeContext alternativeContext => alternativeContext.ToAlternative(grammar),
			_ => throw new("Unknown item type: " + childContext.GetType().Name)
		};
	}

	public static RuleRef ToRuleRef(this RuleRefContext context, Grammar grammar, Quantifier quantifier)
	{
		var name = context.Name().GetText();
		var rule = grammar.Rules.Get(name);
		return new RuleRef {Name = name, Rule = rule, Quantifier = quantifier};
	}

	public static Literal ToLiteral(this LiteralContext context, Quantifier quantifier)
	{
		var terminalNode = context.children.Single().To<ITerminalNode>();
		var (isChar, unescaped) = terminalNode.Symbol.Type switch
		{
			CharLiteral => (true, context.CharLiteral().GetText()[1..^1].Unescape()),
			StringLiteral => (false, context.StringLiteral().GetText()[1..^1].Unescape()),
			_ => throw new("Unknown literal type: " + DefaultVocabulary.GetDisplayName(terminalNode.Symbol.Type))
		};
		return isChar
			? new Literal<Char> {Value = unescaped.Single(), Quantifier = quantifier}
			: new Literal<String> {Value = unescaped, Quantifier = quantifier};
	}

	public static Class ToClass(this ClassContext context, Quantifier quantifier)
	{
		var @class = new Class {Negated = context.not is not null, Quantifier = quantifier};
		foreach (var range in context.range())
			@class.Ranges.Add(range.ToRange());
		return @class;
	}

	public static Range ToRange(this RangeContext context)
	{
		if (context.ShorthandCharacterClass() is { } scc)
			return new RegexCharacterRange {Pattern = scc.Symbol.Text};
		var from = context.Character().First().Symbol.Text.Unescape().Single();
		var to = context.Character().Last().Symbol.Text.Unescape().Single();
		return new CharacterRange {From = from, To = to};
	}

	public static Quantifier ToQuantifier(this QuantifierContext? context)
	{
		var quantifier = context?.children.Single();
		(UInt32 min, UInt32? max) = quantifier switch
		{
			null => (1u, 1u),
			ZeroOrOneContext => (0u, 1u),
			ZeroOrManyContext => (0u, null),
			OneOrManyContext => (1u, null),
			ExactlyContext x => Parse(x.Number(), out var n) ?? (n, n),
			AtLeastContext x => Parse(x.Number(), out var n) ?? (n, null),
			BetweenContext x => Parse(x.min, x.max),
			_ => throw new("Unknown quantifier type: " + quantifier.GetType().Name)
		};
		return new Quantifier {Min = min, Max = max};
	}

	private static (UInt32, UInt32?) Parse(IToken min, IToken max) =>
		(UInt32.Parse(min.Text), UInt32.Parse(max.Text));

	private static (UInt32, UInt32?)? Parse(ITerminalNode node, out UInt32 number)
	{
		number = UInt32.Parse(node.GetText());
		return null;
	}

	private static Char Unescape(Char first, Char second) => (first, second) switch
	{
		('\\', 'n') => '\n',
		('\\', 'r') => '\r',
		('\\', 't') => '\t',
		('\\', '\\') => '\\',
		('\\', '\0') => '\0',
		('\\', _) => second,
		_ => first
	};

	private static Boolean TryUnescape(Char first, Char second, out Char unescaped)
	{
		unescaped = Unescape(first, second);
		return unescaped != first;
	}

	private static String Unescape(this String text)
	{
		var unescaped = new StringBuilder(text.Length);
		for (var i = 0; i < text.Length; i++)
		{
			if (TryUnescape(text[i], i + 1 < text.Length ? text[i + 1] : '\0', out var unescapedChar))
			{
				unescaped.Append(unescapedChar);
				i++;
			}
			else
				unescaped.Append(text[i]);
		}
		return unescaped.ToString();
	}
}

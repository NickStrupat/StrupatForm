using System.Collections.Immutable;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using static StrupatFormParser;

const String path = "grammar.sf";
var text = File.ReadAllText(path);
var stream = new CodePointCharStream(text) {name = path};
var lexer = new StrupatFormLexer(stream);
var tokens = new CommonTokenStream(lexer);
var parser = new StrupatFormParser(tokens);
var grammar = parser.grammar_();
var ruleLookup = grammar.rule_().ToImmutableDictionary(x => x.Name().GetText(), x => x);
var ruleLookupListener = new Listener(ruleLookup);
ParseTreeWalker.Default.Walk(ruleLookupListener, grammar);
;

public partial class StrupatFormParser
{
	public partial class Grammar_Context
	{
		private ImmutableDictionary<String, Rule_Context>? ruleLookup;
		public ImmutableDictionary<String, Rule_Context> RuleLookup => ruleLookup ??= rule_().ToImmutableDictionary(x => x.Name().GetText());
	}

	partial class RuleRefContext
	{
		public Rule_Context? GetRuleContext()
		{
			var grammarContext = this.FindAncestor<Grammar_Context>();
			if (grammarContext is null)
				return null;
			return grammarContext.RuleLookup.TryGetValue(Name().GetText(), out var rule) ? rule : null;
		}
	}
}

static class ParserRuleExtensions
{
	public static TAncestor? FindAncestor<TAncestor>(this RuleContext context) where TAncestor : RuleContext
	{
		for (;;)
			switch (context = context.Parent)
			{
				case TAncestor ancestor: return ancestor;
				case null: return null;
			}
	}
}

sealed class Listener : StrupatFormBaseListener
{
	private readonly ImmutableDictionary<String, Rule_Context> ruleLookup;
	public Listener(ImmutableDictionary<String, Rule_Context> ruleLookup) => this.ruleLookup = ruleLookup;

	public override void EnterRuleRef(RuleRefContext context)
	{
		var rule = context.GetRuleContext();
		if (rule == null)
			Console.Error.WriteLine($"Rule `{context.Name().GetText()}` referenced at {context.Start.Line}:{context.Start.Column} but not defined");
		// if (!ruleLookup.TryGetValue(context.Name().GetText(), out var rule))
		// 	Console.Error.WriteLine($"Rule `{context.Name().GetText()}` referenced at {context.Start.Line}:{context.Start.Column} but not defined");
	}
}

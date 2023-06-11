using System.Collections.Immutable;

namespace StrupatForm;

public partial class StrupatFormParser
{
	public partial class Grammar_Context
	{
		private ImmutableDictionary<String, Rule_Context>? ruleLookup;
		public ImmutableDictionary<String, Rule_Context> RuleLookup => ruleLookup ??= rule_().ToImmutableDictionary(x => x.Name().GetText());
	}

	// partial class RuleRefContext
	// {
	// 	public Rule_Context? GetRuleContext()
	// 	{
	// 		var grammarContext = this.FindAncestor<Grammar_Context>();
	// 		if (grammarContext is null)
	// 			return null;
	// 		return grammarContext.RuleLookup.TryGetValue(Name().GetText(), out var rule) ? rule : null;
	// 	}
	// }
}

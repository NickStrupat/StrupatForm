using Antlr4.Runtime;

namespace StrupatForm;

public class ParserRuleContextEx : ParserRuleContext
{
	protected ParserRuleContextEx() {}
	protected ParserRuleContextEx(ParserRuleContext parent, Int32 invokingStateNumber) : base(parent, invokingStateNumber) {}

	public override String ToString()
	{
		var text = GetText();
		return $"{Start.Line}:{Start.Column}-{Stop.Line}:{Stop.Column} `{(text.Length > 20 ? text[..20] + "..." : text)}`";
	}
}

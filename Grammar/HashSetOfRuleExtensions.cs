namespace StrupatForm;

static class HashSetOfRuleExtensions
{
	public static Rule GetOrAdd(this HashSet<Rule> set, String name)
	{
		var rule = new Rule { Name = name};
		if (set.TryGetValue(rule, out var result))
			return result;
		set.Add(rule);
		return rule;
	}

	public static Rule Get(this HashSet<Rule> set, String name)
	{
		var rule = new Rule { Name = name};
		if (set.TryGetValue(rule, out var result))
			return result;
		throw new KeyNotFoundException($"Rule `{name}` not found");
	}
}

using System.Text;

namespace StrupatForm;

public static class CodeEmitter
{
    public static String Emit(Grammar grammar, String namespaceName, Boolean preserveWhitespace = false)
    {
        DetectLeftRecursion(grammar);

        var w = new IndentedWriter();
        var ctx = new EmitContext(grammar, namespaceName, w, preserveWhitespace);

        EmitPrologue(ctx);

        foreach (var rule in ctx.SortedRules)
        {
            var kind = ctx.GetKind(rule);
            w.Blank();
            EmitRuleComment(rule, ctx);
            switch (kind)
            {
                case RuleKind.Terminal:
                    EmitTerminalRule(rule, ctx);
                    break;
                case RuleKind.Sequence:
                    EmitSequenceRule(rule, ctx);
                    break;
                case RuleKind.Alternative:
                    EmitAlternativeRule(rule, ctx);
                    break;
                case RuleKind.MixedAlternative:
                    EmitMixedAlternativeRule(rule, ctx);
                    break;
            }
        }

        EmitRulesDispatchClass(ctx);
        EmitEpilogue(ctx);
        return w.ToString();
    }

    enum RuleKind { Terminal, Sequence, Alternative, MixedAlternative }

    sealed class EmitContext
    {
        public Grammar Grammar { get; }
        public String Namespace { get; }
        public IndentedWriter W { get; }
        public Boolean PreserveWhitespace { get; }

        readonly Dictionary<Rule, RuleKind> kindMap;
        readonly Dictionary<Rule, String?> firstTokenMap;
        readonly Dictionary<Rule, Byte> ruleIdMap;
        readonly List<Rule> sortedRules;

        public EmitContext(Grammar grammar, String ns, IndentedWriter w, Boolean preserveWhitespace = false)
        {
            Grammar = grammar;
            Namespace = ns;
            W = w;
            PreserveWhitespace = preserveWhitespace;
            kindMap = BuildKindMap(grammar);
            firstTokenMap = BuildFirstTokenMap(grammar);
            sortedRules = TopologicalSort(grammar);
            ruleIdMap = new Dictionary<Rule, Byte>();
            for (var i = 0; i < sortedRules.Count; i++)
                ruleIdMap[sortedRules[i]] = (Byte)(i + 1);
        }

        public RuleKind GetKind(Rule rule) => kindMap[rule];
        public String? GetFirstToken(Rule rule) => firstTokenMap.GetValueOrDefault(rule);
        public Byte GetRuleId(Rule rule) => ruleIdMap[rule];
        public List<Rule> SortedRules => sortedRules;
    }

    // --- Validation ---

    static void DetectLeftRecursion(Grammar grammar)
    {
        foreach (var rule in grammar.Rules.Values)
            DetectLeftRecursion(rule, rule, new HashSet<String>(), grammar);
    }

    static void DetectLeftRecursion(Rule root, Rule current, HashSet<String> visited, Grammar grammar)
    {
        if (!visited.Add(current.Name))
            return;
        foreach (var alt in current.Alternatives)
        {
            if (alt.Items.Count == 0)
                continue;
            if (alt.Items[0] is not RuleRef rr)
                continue;
            if (rr.Name == root.Name)
                throw new InvalidOperationException(
                    $"Left recursion detected: rule '{root.Name}' is reachable from the first item of rule '{current.Name}'. " +
                    $"Rewrite using the pattern: Base (Operator Base)*");
            DetectLeftRecursion(root, rr.Rule, visited, grammar);
        }
    }

    // --- Classification ---

    static Dictionary<Rule, RuleKind> BuildKindMap(Grammar grammar)
    {
        var map = new Dictionary<Rule, RuleKind>();
        foreach (var rule in grammar.Rules.Values)
        {
            if (IsAlternativeRule(rule))
                map[rule] = RuleKind.Alternative;
            else if (IsTerminalRule(rule))
                map[rule] = RuleKind.Terminal;
            else if (rule.Alternatives.Count > 1)
                map[rule] = RuleKind.MixedAlternative;
            else
                map[rule] = RuleKind.Sequence;
        }
        return map;
    }

    static Boolean IsAlternativeRule(Rule rule) =>
        rule.Alternatives.Count > 1 &&
        rule.Alternatives.All(a => a.Items.Count == 1 && a.Items[0] is RuleRef);

    static Boolean IsTerminalRule(Rule rule) =>
        rule.Alternatives.All(a => a.Items.All(i => i is not RuleRef));

    // --- First-token computation ---

    static Dictionary<Rule, String?> BuildFirstTokenMap(Grammar grammar)
    {
        var map = new Dictionary<Rule, String?>();
        foreach (var rule in grammar.Rules.Values)
            map[rule] = ComputeFirstToken(rule, new HashSet<String>());
        return map;
    }

    static String? ComputeFirstToken(Rule rule, HashSet<String> visited)
    {
        if (!visited.Add(rule.Name))
            return null;
        if (rule.Alternatives.Count == 0)
            return null;
        var firstAlt = rule.Alternatives[0];
        if (firstAlt.Items.Count == 0)
            return null;
        var firstItem = firstAlt.Items[0];
        return firstItem switch
        {
            Literal<String> ls => ls.Value,
            RuleRef rr => ComputeFirstToken(rr.Rule, visited),
            _ => null
        };
    }

    // --- Topological sort ---

    static List<Rule> TopologicalSort(Grammar grammar)
    {
        var visited = new HashSet<String>();
        var result = new List<Rule>();

        void Visit(Rule rule)
        {
            if (!visited.Add(rule.Name))
                return;
            foreach (var alt in rule.Alternatives)
                foreach (var item in alt.Items)
                    if (item is RuleRef rr)
                        Visit(rr.Rule);
            result.Add(rule);
        }

        foreach (var rule in grammar.Rules.Values)
            Visit(rule);

        return result;
    }

    // --- Prologue ---

    static void EmitPrologue(EmitContext ctx)
    {
        ctx.W.Lines($$"""
            using Input = System.ReadOnlySpan<System.Char>;

            namespace {{ctx.Namespace}};

            public readonly struct ParseError;
            public sealed class ParseException(ParseError error) : Exception
            {
                public ParseError Error { get; } = error;
            }

            public sealed class UninitializedInstanceException() : Exception("Instance uninitialized");

            public interface IRule
            {
                Input Text { get; }
                void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct;
                void VisitParent<T>(ref T visitor) where T : IVisitor, allows ref struct;
            }

            public interface IVisitor
            {
                void Visit<T>(in T rule) where T : IRule, allows ref struct;
            }
            """);
    }

    // --- Rule comment ---

    static void EmitRuleComment(Rule rule, EmitContext ctx)
    {
        var kind = ctx.GetKind(rule);
        if (kind is RuleKind.Alternative or RuleKind.MixedAlternative)
        {
            ctx.W.Line($"// {rule.Name}");
            foreach (var alt in rule.Alternatives)
            {
                var desc = String.Join(" ", alt.Items.Select(ItemToString));
                ctx.W.Line($"//     {desc}");
            }
        }
        else if (kind == RuleKind.Sequence)
        {
            var desc = String.Join(" ", rule.Alternatives[0].Items.Select(ItemToString));
            ctx.W.Line($"// {rule.Name} -> {desc}");
        }
        else
        {
            var lines = rule.Alternatives.Select(a => String.Join(" ", a.Items.Select(ItemToString)));
            if (rule.Alternatives.Count == 1)
                ctx.W.Line($"// {rule.Name} -> {lines.First()}");
            else
            {
                ctx.W.Line($"// {rule.Name}");
                foreach (var line in lines)
                    ctx.W.Line($"//     {line}");
            }
        }
    }

    static String ItemToString(Item item) => item switch
    {
        RuleRef rr => rr.Name + QuantifierSuffix(rr.Quantifier),
        Literal<String> { Value.Length: 1 } lc => $"'{EscapeChar(lc.Value[0])}'",
        Literal<String> ls => $"\"{ls.Value}\"",
        Class c => ClassToString(c),
        Alternative a => "(" + String.Join(" ", a.Items.Select(ItemToString)) + ")" + QuantifierSuffix(a.Quantifier),
        _ => "?"
    };

    static String ClassToString(Class c)
    {
        var inner = String.Join("", c.Ranges.Select(RangeToString));
        return (c.Negated ? "[^" : "[") + inner + "]";
    }

    static String RangeToString(Range r) => r switch
    {
        CharacterRange cr when cr.From == cr.To => EscapeChar((Char)cr.From.Value),
        CharacterRange cr => $"{EscapeChar((Char)cr.From.Value)}-{EscapeChar((Char)cr.To.Value)}",
        RegexCharacterRange rcr => rcr.Pattern,
        _ => r.ToString() ?? ""
    };

    static String QuantifierSuffix(Quantifier q) => q.ToString() switch
    {
        "1" => "",
        var s => s
    };

    // --- Terminal rules ---

    static void EmitTerminalRule(Rule rule, EmitContext ctx)
    {
        if (IsMultiLiteralTerminal(rule))
        {
            EmitMultiLiteralTerminal(rule, ctx);
            return;
        }

        if (IsSingleLiteralTerminal(rule))
        {
            EmitSingleLiteralTerminal(rule, ctx);
            return;
        }

        EmitCharClassTerminal(rule, ctx);
    }

    static Boolean IsMultiLiteralTerminal(Rule rule) =>
        rule.Alternatives.Count > 1 &&
        rule.Alternatives.All(a => a.Items.Count == 1 && a.Items[0] is Literal<String>);

    static Boolean IsSingleLiteralTerminal(Rule rule) =>
        rule.Alternatives.Count == 1 &&
        rule.Alternatives[0].Items.Count == 1 &&
        rule.Alternatives[0].Items[0] is Literal<String>;

    static void EmitMultiLiteralTerminal(Rule rule, EmitContext ctx)
    {
        var w = ctx.W;
        var literals = rule.Alternatives.Select(a => ((Literal<String>)a.Items[0]).Value).ToList();
        var grouped = literals.GroupBy(l => l.Length).OrderByDescending(g => g.Key).ToList();

        w.Line($"public readonly ref struct {rule.Name} : IRule");
        w.Line("{");
        w.PushIndent();
        w.Lines("""
            private readonly Input input;
            private readonly Input parentInput;
            private readonly Byte parentKind;
            public Int32 Length { get; }
            """);
        w.Blank();
        w.Line($"public {rule.Name}({ConstructorParams()})");
        w.Line("{");
        w.PushIndent();
        w.Lines("""
            this.input = input;
            this.parentInput = parentInput;
            this.parentKind = parentKind;
            """);
        foreach (var group in grouped)
        {
            var len = group.Key;
            var cases = String.Join(" or ", group.Select(l => $"\"{EscapeString(l)}\""));
            w.Line($"if (input.Length >= {len} && input[..{len}] is {cases})");
            w.Line("{");
            w.PushIndent();
            w.Line($"Length = {len};");
            w.Line("return;");
            w.PopIndent();
            w.Line("}");
        }
        w.Line("Length = -1;");
        w.PopIndent();
        w.Line("}");
        w.Blank();
        w.Lines("""
            public Input Text => input[..Length];

            public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }
            """);
        w.Blank();
        EmitVisitParent(ctx);
        w.PopIndent();
        w.Line("}");
    }

    static void EmitSingleLiteralTerminal(Rule rule, EmitContext ctx)
    {
        var w = ctx.W;
        var ns = ctx.Namespace;
        var literal = ((Literal<String>)rule.Alternatives[0].Items[0]).Value;
        var escaped = EscapeString(literal);

        w.Line($"public readonly ref struct {rule.Name} : IRule");
        w.Line("{");
        w.PushIndent();
        w.Lines($$"""
            private readonly Input input;
            private readonly Input parentInput;
            private readonly Byte parentKind;

            public {{rule.Name}}({{ConstructorParams()}})
            {
                this.input = input;
                this.parentInput = parentInput;
                this.parentKind = parentKind;
                if (input.Length < {{literal.Length}} || input[..{{literal.Length}}] is not "{{escaped}}")
                    { Length = -1; return; }
                Length = {{literal.Length}};
            }

            public Input Text => input[..Length];
            public Int32 Length { get; }

            public void VisitChildren<T>(ref T visitor) where T : {{ns}}.IVisitor, allows ref struct { }
            """);
        w.Blank();
        EmitVisitParent(ctx, qualifyIVisitor: true);
        w.Blank();
        w.Lines($$"""
            public static void Parse<T>(Input input, T visitor) where T : IVisitor
            {
                switch (input)
                {
                    case "{{escaped}}": visitor.Visit(new {{rule.Name}}(input)); break;
                    default: visitor.Visit(new ParseError()); break;
                }
            }

            public interface IVisitor
            {
                void Visit(in ParseError parseError);
                void Visit(in {{rule.Name}} {{CamelCase(rule.Name)}});
            }
            """);
        w.PopIndent();
        w.Line("}");
    }

    static void EmitCharClassTerminal(Rule rule, EmitContext ctx)
    {
        var w = ctx.W;

        w.Line($"public readonly ref struct {rule.Name} : IRule");
        w.Line("{");
        w.PushIndent();
        w.Lines("""
            private readonly Input input;
            private readonly Input parentInput;
            private readonly Byte parentKind;
            public Int32 Length { get; }
            """);
        w.Blank();
        w.Line($"public {rule.Name}({ConstructorParams()})");
        w.Line("{");
        w.PushIndent();
        w.Lines("""
            this.input = input;
            this.parentInput = parentInput;
            this.parentKind = parentKind;
            """);

        if (rule.Alternatives.Count == 1)
            EmitCharClassSingleAlternative(rule.Alternatives[0], rule, ctx);
        else
            EmitCharClassMultiAlternative(rule, ctx);

        w.PopIndent();
        w.Line("}");
        w.Blank();
        w.Lines("""
            public Input Text => input[..Length];

            public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }
            """);
        w.Blank();
        EmitVisitParent(ctx);
        EmitCharClassHelperMethods(rule, ctx);
        w.PopIndent();
        w.Line("}");
    }

    static void EmitCharClassMultiAlternative(Rule rule, EmitContext ctx)
    {
        var w = ctx.W;

        for (var i = 0; i < rule.Alternatives.Count; i++)
        {
            var alt = rule.Alternatives[i];
            var isLast = i == rule.Alternatives.Count - 1;
            var condition = GetLeadingCondition(alt);

            if (i == 0)
                w.Line($"if ({condition})");
            else if (!isLast)
                w.Line($"else if ({condition})");
            else
                w.Line("else");

            w.Line("{");
            w.PushIndent();
            EmitCharClassSingleAlternative(alt, rule, ctx);
            w.PopIndent();
            w.Line("}");
        }
    }

    static String GetLeadingCondition(Alternative alt)
    {
        var first = alt.Items[0];
        if (first is Literal<String> ls)
        {
            if (ls.Value.Length > 1)
                return $"input.Length > 1 && input[0] == '{EscapeChar(ls.Value[0])}' && (input[1] == '{EscapeChar(ls.Value[1])}')";
            return $"input.Length > 0 && input[0] == '{EscapeChar(ls.Value[0])}'";
        }
        if (first is Class c)
        {
            var preds = c.Ranges.Select(r => RangeToCondition(r, "input[0]")).ToList();
            var combined = String.Join(" || ", preds);
            if (c.Negated) combined = $"!({combined})";
            return $"input.Length > 0 && ({combined})";
        }
        return "true";
    }

    static void EmitCharClassSingleAlternative(Alternative alt, Rule rule, EmitContext ctx)
    {
        var w = ctx.W;
        var items = alt.Items;

        if (items.Count == 1)
        {
            EmitSingleItemTerminal(items[0], ctx);
            return;
        }

        if (IsDelimitedPattern(items))
        {
            EmitDelimitedPattern(items, ctx);
            return;
        }

        w.Line("var i = 0;");
        foreach (var item in items)
        {
            if (item is Literal<String> ls)
            {
                if (ls.Value.Length == 1 && ls.Quantifier is { Min: 1, Max: 1 })
                {
                    w.Line($"if (i >= input.Length || input[i] != '{EscapeChar(ls.Value[0])}')");
                    w.PushIndent();
                    w.Line("{ Length = -1; return; }");
                    w.PopIndent();
                    w.Line("i++;");
                }
                else if (ls.Value.Length > 1)
                {
                    w.Line($"if (!input[i..].StartsWith(\"{EscapeString(ls.Value)}\"))");
                    w.PushIndent();
                    w.Line("{ Length = -1; return; }");
                    w.PopIndent();
                    w.Line($"i += {ls.Value.Length};");
                }
            }
            else if (item is Class c)
            {
                var helperName = GetClassHelperName(c);
                var q = c.Quantifier;
                if (q is { Min: 1, Max: 1 })
                {
                    w.Line($"if (i >= input.Length || !{helperName}(input[i]))");
                    w.PushIndent();
                    w.Line("{ Length = -1; return; }");
                    w.PopIndent();
                    w.Line("i++;");
                }
                else if (q is { Min: 1, Max: null })
                {
                    w.Line($"if (i >= input.Length || !{helperName}(input[i]))");
                    w.PushIndent();
                    w.Line("{ Length = -1; return; }");
                    w.PopIndent();
                    w.Line("i++;");
                    w.Line($"while (i < input.Length && {helperName}(input[i]))");
                    w.PushIndent();
                    w.Line("i++;");
                    w.PopIndent();
                }
                else if (q is { Min: 0, Max: null })
                {
                    w.Line($"while (i < input.Length && {helperName}(input[i]))");
                    w.PushIndent();
                    w.Line("i++;");
                    w.PopIndent();
                }
            }
        }
        w.Line("Length = i;");
    }

    static Boolean IsDelimitedPattern(List<Item> items) =>
        items.Count == 3 &&
        items[0] is Literal<String> { Value.Length: 1 } open &&
        items[1] is Class { Negated: true } &&
        items[2] is Literal<String> { Value.Length: 1 } close &&
        open.Value == close.Value;

    static void EmitDelimitedPattern(List<Item> items, EmitContext ctx)
    {
        var w = ctx.W;
        var delim = ((Literal<String>)items[0]).Value[0];
        w.Line($"var end = input[1..].IndexOf('{EscapeChar(delim)}');");
        w.Line("if (end < 0)");
        w.PushIndent();
        w.Line("{ Length = -1; return; }");
        w.PopIndent();
        w.Line("Length = end + 2;");
    }

    static void EmitSingleItemTerminal(Item item, EmitContext ctx) =>
        EmitSingleItemTerminalWithFailAction(item, "{ Length = -1; return; }", ctx);

    static void EmitSingleItemTerminalWithFailAction(Item item, String failAction, EmitContext ctx)
    {
        var w = ctx.W;
        if (item is Literal<String> ls)
        {
            if (ls.Value.Length == 1)
            {
                w.Line($"if (input.Length == 0 || input[0] != '{EscapeChar(ls.Value[0])}')");
                w.PushIndent();
                w.Line(failAction);
                w.PopIndent();
                w.Line("Length = 1;");
            }
            else
            {
                w.Line($"if (!input.StartsWith(\"{EscapeString(ls.Value)}\"))");
                w.PushIndent();
                w.Line(failAction);
                w.PopIndent();
                w.Line($"Length = {ls.Value.Length};");
            }
        }
        else if (item is Class c)
        {
            var helperName = GetClassHelperName(c);
            var q = c.Quantifier;
            if (q is { Min: 1, Max: 1 })
            {
                w.Line($"if (input.Length == 0 || !{helperName}(input[0]))");
                w.PushIndent();
                w.Line(failAction);
                w.PopIndent();
                w.Line("Length = 1;");
            }
            else if (q is { Min: 1, Max: null })
            {
                w.Line($"if (input.Length == 0 || !{helperName}(input[0]))");
                w.PushIndent();
                w.Line(failAction);
                w.PopIndent();
                w.Line("var i = 1;");
                w.Line($"while (i < input.Length && {helperName}(input[i]))");
                w.PushIndent();
                w.Line("i++;");
                w.PopIndent();
                w.Line("Length = i;");
            }
            else if (q is { Min: 0, Max: null })
            {
                w.Line("var i = 0;");
                w.Line($"while (i < input.Length && {helperName}(input[i]))");
                w.PushIndent();
                w.Line("i++;");
                w.PopIndent();
                w.Line("Length = i;");
            }
        }
    }

    static void EmitCharClassHelperMethods(Rule rule, EmitContext ctx)
    {
        var w = ctx.W;
        var emitted = new HashSet<String>();

        foreach (var alt in rule.Alternatives)
            foreach (var item in alt.Items)
                if (item is Class c)
                {
                    var name = GetClassHelperName(c);
                    if (emitted.Add(name))
                    {
                        w.Blank();
                        var preds = c.Ranges.Select(r => RangeToCondition(r, "c")).ToList();
                        var combined = String.Join(" || ", preds);
                        if (c.Negated) combined = $"!({combined})";
                        w.Line($"private static Boolean {name}(Char c) =>");
                        w.PushIndent();
                        w.Line($"{combined};");
                        w.PopIndent();
                    }
                }
    }

    static String RangeToCondition(Range r, String charExpr) => r switch
    {
        CharacterRange cr when cr.From == cr.To && IsShorthandChar(cr) => ShorthandCondition(cr, charExpr),
        CharacterRange cr when cr.From == cr.To => $"{charExpr} == '{EscapeChar((Char)cr.From.Value)}'",
        CharacterRange cr => $"({charExpr} >= '{EscapeChar((Char)cr.From.Value)}' && {charExpr} <= '{EscapeChar((Char)cr.To.Value)}')",
        RegexCharacterRange rcr => RegexRangeToCondition(rcr, charExpr),
        _ => "true"
    };

    static String RegexRangeToCondition(RegexCharacterRange rcr, String charExpr) => rcr.Pattern switch
    {
        "\\w" => $"(Char.IsLetter({charExpr}) || {charExpr} == '_')",
        "\\W" => $"(!Char.IsLetter({charExpr}) && {charExpr} != '_')",
        "\\d" => $"Char.IsAsciiDigit({charExpr})",
        "\\D" => $"!Char.IsAsciiDigit({charExpr})",
        "\\s" => $"Char.IsWhiteSpace({charExpr})",
        "\\S" => $"!Char.IsWhiteSpace({charExpr})",
        _ when rcr.Pattern.StartsWith("\\p{") => UnicodeCategoryToCondition(rcr.Pattern[3..^1], charExpr),
        _ => $"/* unsupported: {rcr.Pattern} */ true"
    };

    static String UnicodeCategoryToCondition(String property, String charExpr)
    {
        if (property.StartsWith("General_Category="))
            property = property["General_Category=".Length..];

        return property switch
        {
            "L" or "Letter" => $"Char.IsLetter({charExpr})",
            "Lu" or "Uppercase_Letter" => $"Char.IsUpper({charExpr})",
            "Ll" or "Lowercase_Letter" => $"Char.IsLower({charExpr})",
            "N" or "Number" => $"Char.IsNumber({charExpr})",
            "Nd" or "Decimal_Number" => $"Char.IsDigit({charExpr})",
            "P" or "Punctuation" => $"Char.IsPunctuation({charExpr})",
            "S" or "Symbol" => $"Char.IsSymbol({charExpr})",
            "Z" or "Separator" => $"Char.IsSeparator({charExpr})",
            "C" or "Other" => $"Char.IsControl({charExpr})",
            "Cc" or "Control" => $"Char.IsControl({charExpr})",
            "M" or "Mark" => $"(Char.GetUnicodeCategory({charExpr}) is System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark or System.Globalization.UnicodeCategory.EnclosingMark)",
            "Alpha" or "Alphabetic" => $"Char.IsLetter({charExpr})",
            "Upper" or "Uppercase" => $"Char.IsUpper({charExpr})",
            "Lower" or "Lowercase" => $"Char.IsLower({charExpr})",
            "White_Space" => $"Char.IsWhiteSpace({charExpr})",
            "Lt" or "Titlecase_Letter" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.TitlecaseLetter)",
            "Lm" or "Modifier_Letter" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.ModifierLetter)",
            "Lo" or "Other_Letter" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.OtherLetter)",
            "Mn" or "Nonspacing_Mark" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.NonSpacingMark)",
            "Mc" or "Spacing_Mark" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.SpacingCombiningMark)",
            "Me" or "Enclosing_Mark" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.EnclosingMark)",
            "Nl" or "Letter_Number" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.LetterNumber)",
            "No" or "Other_Number" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.OtherNumber)",
            "Pc" or "Connector_Punctuation" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.ConnectorPunctuation)",
            "Pd" or "Dash_Punctuation" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.DashPunctuation)",
            "Ps" or "Open_Punctuation" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.OpenPunctuation)",
            "Pe" or "Close_Punctuation" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.ClosePunctuation)",
            "Pi" or "Initial_Punctuation" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.InitialQuotePunctuation)",
            "Pf" or "Final_Punctuation" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.FinalQuotePunctuation)",
            "Po" or "Other_Punctuation" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.OtherPunctuation)",
            "Sm" or "Math_Symbol" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.MathSymbol)",
            "Sc" or "Currency_Symbol" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.CurrencySymbol)",
            "Sk" or "Modifier_Symbol" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.ModifierSymbol)",
            "So" or "Other_Symbol" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.OtherSymbol)",
            "Zs" or "Space_Separator" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.SpaceSeparator)",
            "Zl" or "Line_Separator" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.LineSeparator)",
            "Zp" or "Paragraph_Separator" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.ParagraphSeparator)",
            "Cf" or "Format" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.Format)",
            "Cs" or "Surrogate" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.Surrogate)",
            "Co" or "Private_Use" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.PrivateUse)",
            "Cn" or "Unassigned" => $"(Char.GetUnicodeCategory({charExpr}) == System.Globalization.UnicodeCategory.OtherNotAssigned)",
            _ => $"/* unsupported unicode property: {property} */ true"
        };
    }

    static Boolean IsShorthandChar(CharacterRange cr) =>
        cr.From.Value is 'w' or 'd' or 's';

    static String ShorthandCondition(CharacterRange cr, String charExpr) => (Char)cr.From.Value switch
    {
        'w' => $"(Char.IsLetter({charExpr}) || {charExpr} == '_')",
        'd' => $"Char.IsAsciiDigit({charExpr})",
        's' => $"Char.IsWhiteSpace({charExpr})",
        _ => $"{charExpr} == '{EscapeChar((Char)cr.From.Value)}'"
    };

    static String RegexRangeToName(RegexCharacterRange rcr) => rcr.Pattern switch
    {
        "\\w" => "Word",
        "\\d" => "Digit",
        "\\s" => "Space",
        "\\W" => "NonWord",
        "\\D" => "NonDigit",
        "\\S" => "NonSpace",
        _ when rcr.Pattern.StartsWith("\\p{") => rcr.Pattern[3..^1].Replace("=", "").Replace("_", ""),
        _ => "Custom"
    };

    static String GetClassHelperName(Class c)
    {
        var parts = c.Ranges.Select(r => r switch
        {
            RegexCharacterRange rcr => RegexRangeToName(rcr),
            CharacterRange cr when cr.From == cr.To && IsShorthandChar(cr) => ShorthandName(cr),
            CharacterRange cr when cr.From == cr.To => CharName((Char)cr.From.Value),
            CharacterRange cr => $"{CharName((Char)cr.From.Value)}To{CharName((Char)cr.To.Value)}",
            _ => "Unknown"
        });
        var desc = String.Join("", parts);
        if (c.Negated) desc = "Not" + desc;
        return $"Is{desc}Char";
    }

    static String ShorthandName(CharacterRange cr) => (Char)cr.From.Value switch
    {
        'w' => "Word",
        'd' => "Digit",
        's' => "Space",
        _ => CharName((Char)cr.From.Value)
    };

    static String CharName(Char c) => c switch
    {
        >= 'a' and <= 'z' => c.ToString().ToUpperInvariant(),
        >= 'A' and <= 'Z' => c.ToString(),
        >= '0' and <= '9' => c.ToString(),
        _ => ((Int32)c).ToString("X2")
    };

    // --- Sequence rules ---

    static void EmitSequenceRule(Rule rule, EmitContext ctx)
    {
        var w = ctx.W;
        var alt = rule.Alternatives[0];
        var items = alt.Items;

        var nonTerminals = items.Where(i => i is RuleRef rr && (rr.Quantifier is { Min: 1, Max: 1 } || rr.Quantifier is { Min: 0, Max: 1 })).Cast<RuleRef>().ToList();
        var propertyNames = AssignPropertyNames(nonTerminals);

        w.Line($"public readonly ref struct {rule.Name} : IRule");
        w.Line("{");
        w.PushIndent();
        w.Line("private readonly Input input;");
        EmitParentFields(ctx);

        foreach (var (_, name) in nonTerminals.Zip(propertyNames))
            w.Line($"private readonly Int32 {CamelCase(name)}Start;");
        w.Line("public Int32 Length { get; }");

        w.Blank();
        w.Line($"public {rule.Name}({ConstructorParams()})");
        w.Line("{");
        w.PushIndent();
        w.Line("this.input = input;");
        EmitParentAssignment(ctx);

        EmitSequenceParseBody(items, nonTerminals, propertyNames, ctx);

        w.PopIndent();
        w.Line("}");

        w.Blank();
        foreach (var (rr, name) in nonTerminals.Zip(propertyNames))
            w.Line($"public {rr.Name} {name} => new(input[{CamelCase(name)}Start..]);");
        w.Line("public Input Text => input[..Length];");

        w.Blank();
        var hasRepetition = items.Any(i =>
            (i is Alternative { Quantifier.Max: null }) ||
            (i is RuleRef rr2 && rr2.Quantifier.Max == null));
        var ruleId = ctx.GetRuleId(rule);
        w.Line("public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct");
        w.Line("{");
        w.PushIndent();
        if (hasRepetition)
        {
            EmitSequenceVisitChildrenWithRepetition(items, nonTerminals, propertyNames, ruleId, ctx);
        }
        else
        {
            foreach (var (rr, name) in nonTerminals.Zip(propertyNames))
            {
                if (rr.Quantifier is { Min: 0, Max: 1 })
                {
                    w.Line($"var {CamelCase(name)}Opt = new {rr.Name}(input[{CamelCase(name)}Start..], input, {ruleId});");
                    w.Line($"if ({CamelCase(name)}Opt.Length >= 0) visitor.Visit({CamelCase(name)}Opt);");
                }
                else
                {
                    w.Line($"visitor.Visit(new {rr.Name}(input[{CamelCase(name)}Start..], input, {ruleId}));");
                }
            }
        }
        w.PopIndent();
        w.Line("}");

        w.Blank();
        EmitVisitParent(ctx);

        if (items.Count > 1)
        {
            w.Blank();
            EmitSkipWhitespace(ctx);
        }

        w.PopIndent();
        w.Line("}");
    }

    static void EmitSequenceParseBody(List<Item> items, List<RuleRef> nonTerminals, List<String> propertyNames, EmitContext ctx)
    {
        var w = ctx.W;
        w.Line("var pos = 0;");

        var ntIndex = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isLast = i == items.Count - 1;

            if (item is Literal<String> { Value.Length: 1 } lc)
            {
                if (lc.Quantifier is { Min: 0, Max: 1 })
                {
                    w.Line($"if (pos < input.Length && input[pos] == '{EscapeChar(lc.Value[0])}')");
                    w.PushIndent();
                    w.Line("pos += 1;");
                    w.PopIndent();
                }
                else
                {
                    w.Line($"if (pos >= input.Length || input[pos] != '{EscapeChar(lc.Value[0])}')");
                    w.PushIndent();
                    w.Line("{ Length = -1; return; }");
                    w.PopIndent();
                    w.Line("pos += 1;");
                }
                if (!isLast) EmitSkipWs(ctx);
            }
            else if (item is Literal<String> ls)
            {
                w.Line($"if (!input[pos..].StartsWith(\"{EscapeString(ls.Value)}\"))");
                w.PushIndent();
                w.Line("{ Length = -1; return; }");
                w.PopIndent();
                w.Line($"pos += {ls.Value.Length};");
                if (!isLast) EmitSkipWs(ctx);
            }
            else if (item is RuleRef rr)
            {
                if (rr.Quantifier is { Min: 1, Max: 1 })
                {
                    var name = propertyNames[ntIndex];
                    w.Line($"{CamelCase(name)}Start = pos;");
                    w.Line($"var {CamelCase(name)} = new {rr.Name}(input[pos..]);");
                    w.Line($"if ({CamelCase(name)}.Length < 0) {{ Length = -1; return; }}");
                    w.Line($"pos += {CamelCase(name)}.Length;");
                    if (!isLast) EmitSkipWs(ctx);
                    ntIndex++;
                }
                else if (rr.Quantifier is { Min: 0, Max: 1 })
                {
                    var name = propertyNames[ntIndex];
                    w.Line($"{CamelCase(name)}Start = pos;");
                    w.Line($"var {CamelCase(name)} = new {rr.Name}(input[pos..]);");
                    w.Line($"if ({CamelCase(name)}.Length >= 0) pos += {CamelCase(name)}.Length;");
                    if (!isLast) EmitSkipWs(ctx);
                    ntIndex++;
                }
                else if (rr.Quantifier.Max == null)
                {
                    if (rr.Quantifier.Min >= 1)
                    {
                        w.Line($"var first{rr.Name} = new {rr.Name}(input[pos..]);");
                        w.Line($"if (first{rr.Name}.Length < 0) {{ Length = -1; return; }}");
                        w.Line($"pos += first{rr.Name}.Length;");
                    }
                    w.Line("while (true)");
                    w.Line("{");
                    w.PushIndent();
                    w.Line($"var next{rr.Name} = new {rr.Name}(input[pos..]);");
                    w.Line($"if (next{rr.Name}.Length < 0) break;");
                    w.Line($"pos += next{rr.Name}.Length;");
                    w.PopIndent();
                    w.Line("}");
                    if (!isLast) EmitSkipWs(ctx);
                }
            }
            else if (item is Alternative subAlt && subAlt.Quantifier.Max == null)
            {
                EmitInlineGroupRepetition(subAlt, ctx);
            }
        }

        w.Line("Length = pos;");
    }

    static void EmitSequenceVisitChildrenWithRepetition(List<Item> items, List<RuleRef> nonTerminals, List<String> propertyNames, Byte ruleId, EmitContext ctx)
    {
        var w = ctx.W;
        var ntIndex = 0;
        var declaredPos = false;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item is RuleRef rr && (rr.Quantifier is { Min: 1, Max: 1 } || rr.Quantifier is { Min: 0, Max: 1 }))
            {
                var name = propertyNames[ntIndex];
                w.Line($"var {CamelCase(name)} = new {rr.Name}(input[{CamelCase(name)}Start..], input, {ruleId});");
                w.Line($"visitor.Visit({CamelCase(name)});");
                if (!declaredPos)
                {
                    w.Line($"var pos = {CamelCase(name)}Start + {CamelCase(name)}.Length;");
                    declaredPos = true;
                }
                else
                {
                    w.Line($"pos = {CamelCase(name)}Start + {CamelCase(name)}.Length;");
                }
                ntIndex++;
            }
            else if (item is RuleRef qrr && qrr.Quantifier.Max == null)
            {
                if (!declaredPos)
                {
                    w.Line("var pos = 0;");
                    declaredPos = true;
                }
                w.Line("while (pos < Length)");
                w.Line("{");
                w.PushIndent();
                w.Line($"var next{qrr.Name} = new {qrr.Name}(input[pos..], input, {ruleId});");
                w.Line($"if (next{qrr.Name}.Length < 0) break;");
                w.Line($"visitor.Visit(next{qrr.Name});");
                w.Line($"pos += next{qrr.Name}.Length;");
                w.PopIndent();
                w.Line("}");
            }
            else if (item is Alternative subAlt && subAlt.Quantifier.Max == null)
            {
                if (!declaredPos)
                {
                    w.Line("var pos = 0;");
                    declaredPos = true;
                }
                w.Line("while (pos < Length)");
                w.Line("{");
                w.PushIndent();
                w.Line("var savedPos = pos;");
                EmitSkipWs(ctx);
                foreach (var subItem in subAlt.Items)
                {
                    if (subItem is RuleRef subRr)
                    {
                        w.Line($"var next{subRr.Name} = new {subRr.Name}(input[pos..], input, {ruleId});");
                        w.Line($"if (next{subRr.Name}.Length < 0) {{ pos = savedPos; break; }}");
                        w.Line($"visitor.Visit(next{subRr.Name});");
                        w.Line($"pos += next{subRr.Name}.Length;");
                        EmitSkipWs(ctx);
                    }
                    else if (subItem is Literal<String> { Value.Length: 1 } subLc)
                    {
                        w.Line($"if (pos >= input.Length || input[pos] != '{EscapeChar(subLc.Value[0])}') {{ pos = savedPos; break; }}");
                        w.Line("pos += 1;");
                        if (subLc.Quantifier is { Max: null })
                        {
                            w.Line($"while (pos < input.Length && input[pos] == '{EscapeChar(subLc.Value[0])}')");
                            w.PushIndent();
                            w.Line("pos += 1;");
                            w.PopIndent();
                        }
                    }
                    else if (subItem is Literal<String> subLs)
                    {
                        w.Line($"if (!input[pos..].StartsWith(\"{EscapeString(subLs.Value)}\")) {{ pos = savedPos; break; }}");
                        w.Line($"pos += {subLs.Value.Length};");
                    }
                }
                w.PopIndent();
                w.Line("}");
            }
        }
    }

    static void EmitInlineGroupRepetition(Alternative subAlt, EmitContext ctx)
    {
        var w = ctx.W;
        var items = subAlt.Items;

        if (items.Count == 2 && items[0] is Literal<String> { Value.Length: 1 } lc && items[1] is RuleRef singleRr)
        {
            w.Line($"while (pos < input.Length && input[pos] == '{EscapeChar(lc.Value[0])}')");
            w.Line("{");
            w.PushIndent();
            w.Line("pos += 1;");
            w.Line($"var next = new {singleRr.Name}(input[pos..]);");
            w.Line("if (next.Length < 0) break;");
            w.Line("pos += next.Length;");
            w.PopIndent();
            w.Line("}");
            return;
        }

        w.Line("while (true)");
        w.Line("{");
        w.PushIndent();
        w.Line("var savedPos = pos;");
        EmitSkipWs(ctx);
        foreach (var item in items)
        {
            if (item is RuleRef rr)
            {
                var varName = $"next{rr.Name}";
                w.Line($"var {varName} = new {rr.Name}(input[pos..]);");
                w.Line($"if ({varName}.Length < 0) {{ pos = savedPos; break; }}");
                w.Line($"pos += {varName}.Length;");
                EmitSkipWs(ctx);
            }
            else if (item is Literal<String> ls)
            {
                w.Line($"if (!input[pos..].StartsWith(\"{EscapeString(ls.Value)}\"))");
                w.PushIndent();
                w.Line("{ pos = savedPos; break; }");
                w.PopIndent();
                w.Line($"pos += {ls.Value.Length};");
                EmitSkipWs(ctx);
            }
            else if (item is Literal<String> { Value.Length: 1 } litC)
            {
                var q = litC.Quantifier;
                w.Line($"if (pos >= input.Length || input[pos] != '{EscapeChar(litC.Value[0])}')");
                w.PushIndent();
                w.Line("{ pos = savedPos; break; }");
                w.PopIndent();
                w.Line("pos += 1;");
                if (q is { Max: null })
                {
                    w.Line($"while (pos < input.Length && input[pos] == '{EscapeChar(litC.Value[0])}')");
                    w.PushIndent();
                    w.Line("pos += 1;");
                    w.PopIndent();
                }
                EmitSkipWs(ctx);
            }
        }
        w.PopIndent();
        w.Line("}");
    }

    // --- Mixed alternative rules ---

    static void EmitMixedAlternativeRule(Rule rule, EmitContext ctx)
    {
        var w = ctx.W;

        w.Line($"public readonly ref struct {rule.Name} : IRule");
        w.Line("{");
        w.PushIndent();
        w.Lines("""
            private readonly Input input;
            private readonly Input parentInput;
            private readonly Byte parentKind;
            private readonly Byte index;
            public Int32 Length { get; }
            """);
        w.Blank();
        w.Line($"public {rule.Name}({ConstructorParams()})");
        w.Line("{");
        w.PushIndent();
        w.Lines("""
            this.input = input;
            this.parentInput = parentInput;
            this.parentKind = parentKind;
            """);

        for (var i = 0; i < rule.Alternatives.Count; i++)
        {
            var alt = rule.Alternatives[i];
            var isLast = i == rule.Alternatives.Count - 1;

            if (!isLast)
            {
                w.Line("do");
                w.Line("{");
                w.PushIndent();
            }

            EmitMixedAlternativeBody(alt, i + 1, ctx, breakOnFailure: !isLast);

            if (!isLast)
            {
                w.PopIndent();
                w.Line("} while (false);");
                w.Line("if (Length >= 0) return;");
            }
        }

        w.PopIndent();
        w.Line("}");
        w.Blank();
        w.Lines("""
            public Input Text => input[..Length];

            public void VisitChildren<T>(ref T visitor) where T : IVisitor, allows ref struct { }
            """);
        w.Blank();
        EmitVisitParent(ctx);
        if (rule.Alternatives.Any(a => a.Items.Any(i => i is Class)))
            EmitCharClassHelperMethods(rule, ctx);
        w.PopIndent();
        w.Line("}");
    }

    static void EmitMixedAlternativeBody(Alternative alt, Int32 index, EmitContext ctx, Boolean breakOnFailure = false)
    {
        var w = ctx.W;
        var items = alt.Items;
        var failAction = breakOnFailure ? "{ Length = -1; break; }" : "{ Length = -1; return; }";

        if (items.Count == 1 && items[0] is not RuleRef)
        {
            EmitSingleItemTerminalWithFailAction(items[0], failAction, ctx);
            w.Line($"index = {index};");
            return;
        }

        w.Line("var pos = 0;");
        var varCounts = new Dictionary<String, Int32>();
        foreach (var item in items)
        {
            if (item is Literal<String> ls)
            {
                if (ls.Value.Length == 1)
                {
                    w.Line($"if (pos >= input.Length || input[pos] != '{EscapeChar(ls.Value[0])}')");
                    w.PushIndent();
                    w.Line(failAction);
                    w.PopIndent();
                    w.Line("pos += 1;");
                }
                else
                {
                    w.Line($"if (!input[pos..].StartsWith(\"{EscapeString(ls.Value)}\"))");
                    w.PushIndent();
                    w.Line(failAction);
                    w.PopIndent();
                    w.Line($"pos += {ls.Value.Length};");
                }
            }
            else if (item is RuleRef rr)
            {
                varCounts.TryGetValue(rr.Name, out var count);
                varCounts[rr.Name] = count + 1;
                var varName = count == 0 ? CamelCase(rr.Name) : $"{CamelCase(rr.Name)}{count + 1}";
                var q = rr.Quantifier;
                if (q is { Min: 1, Max: 1 })
                {
                    w.Line($"var {varName} = new {rr.Name}(input[pos..]);");
                    w.Line($"if ({varName}.Length < 0) {failAction}");
                    w.Line($"pos += {varName}.Length;");
                }
                else if (q is { Min: 0, Max: 1 })
                {
                    w.Line($"var {varName} = new {rr.Name}(input[pos..]);");
                    w.Line($"if ({varName}.Length >= 0) pos += {varName}.Length;");
                }
                else if (q.Max == null)
                {
                    if (q.Min >= 1)
                    {
                        w.Line($"var first{rr.Name} = new {rr.Name}(input[pos..]);");
                        w.Line($"if (first{rr.Name}.Length < 0) {failAction}");
                        w.Line($"pos += first{rr.Name}.Length;");
                    }
                    w.Line("while (true)");
                    w.Line("{");
                    w.PushIndent();
                    w.Line($"var next{rr.Name} = new {rr.Name}(input[pos..]);");
                    w.Line($"if (next{rr.Name}.Length < 0) break;");
                    w.Line($"pos += next{rr.Name}.Length;");
                    w.PopIndent();
                    w.Line("}");
                }
            }
            else if (item is Class c)
            {
                var helperName = GetClassHelperName(c);
                var q = c.Quantifier;
                if (q is { Min: 1, Max: 1 })
                {
                    w.Line($"if (pos >= input.Length || !{helperName}(input[pos]))");
                    w.PushIndent();
                    w.Line(failAction);
                    w.PopIndent();
                    w.Line("pos += 1;");
                }
                else if (q is { Min: 1, Max: null })
                {
                    w.Line($"if (pos >= input.Length || !{helperName}(input[pos]))");
                    w.PushIndent();
                    w.Line(failAction);
                    w.PopIndent();
                    w.Line("pos += 1;");
                    w.Line($"while (pos < input.Length && {helperName}(input[pos]))");
                    w.PushIndent();
                    w.Line("pos += 1;");
                    w.PopIndent();
                }
                else if (q is { Min: 0, Max: null })
                {
                    w.Line($"while (pos < input.Length && {helperName}(input[pos]))");
                    w.PushIndent();
                    w.Line("pos += 1;");
                    w.PopIndent();
                }
            }
        }
        w.Line($"index = {index};");
        w.Line("Length = pos;");
    }

    // --- Alternative rules ---

    static void EmitAlternativeRule(Rule rule, EmitContext ctx)
    {
        var w = ctx.W;
        var alternatives = rule.Alternatives
            .Select(a => (RuleRef: (RuleRef)a.Items[0], Alt: a))
            .ToList();

        w.Line($"public readonly ref struct {rule.Name} : IRule");
        w.Line("{");
        w.PushIndent();
        w.Line("private readonly Input input;");
        EmitParentFields(ctx);
        w.Line("private readonly Byte index;");
        w.Line("public Int32 Length { get; }");

        w.Blank();
        w.Line($"public {rule.Name}({ConstructorParams()})");
        w.Line("{");
        w.PushIndent();
        w.Line("this.input = input;");
        EmitParentAssignment(ctx);

        EmitAlternativeConstructorBody(rule, alternatives, ctx);

        w.PopIndent();
        w.Line("}");

        w.Blank();
        w.Line("public Input Text => input[..Length];");

        w.Blank();
        EmitAlternativeVisitChildren(rule, alternatives, ctx);

        w.Blank();
        EmitAlternativeVisitMethod(rule, alternatives, ctx);

        w.Blank();
        EmitVisitParent(ctx, qualifyIVisitor: true);

        w.Blank();
        EmitAlternativeIVisitor(rule, alternatives, ctx);

        if (alternatives.Any(x => x.RuleRef.Quantifier.Max == null))
        {
            w.Blank();
            EmitSkipWhitespace(ctx);
        }

        w.PopIndent();
        w.Line("}");

        foreach (var (rr, _) in alternatives.Where(x => x.RuleRef.Quantifier.Max == null))
        {
            w.Blank();
            EmitRepetitionEnumerable(rr, ctx);
        }
    }

    static void EmitAlternativeConstructorBody(Rule rule, List<(RuleRef RuleRef, Alternative Alt)> alternatives, EmitContext ctx)
    {
        var w = ctx.W;

        var groups = ClassifyAlternatives(alternatives, ctx);
        var isFirst = true;

        foreach (var (rr, index, cond) in groups.withKeyword)
        {
            EmitAlternativeBranch(cond, rr, index, isFirst, ctx);
            isFirst = false;
        }

        if (groups.ambiguous.Count == 0)
        {
            if (!isFirst)
            {
                w.Line("else");
                w.Line("{");
                w.PushIndent();
                w.Line("index = Byte.MaxValue;");
                w.Line("Length = 0;");
                w.PopIndent();
                w.Line("}");
            }
            else
            {
                w.Line("index = Byte.MaxValue;");
            }
        }
        else if (groups.ambiguous.Count == 1)
        {
            var (rr, index) = groups.ambiguous[0];
            if (!isFirst) w.Line("else");
            w.Line("{");
            w.PushIndent();
            EmitAlternativeConstruction(rr, index, ctx);
            w.PopIndent();
            w.Line("}");
        }
        else
        {
            if (!isFirst)
            {
                w.Line("else");
                w.Line("{");
                w.PushIndent();
            }

            EmitAmbiguousTryCatchChain(groups.ambiguous, 0, ctx);

            if (!isFirst)
            {
                w.PopIndent();
                w.Line("}");
            }
        }
    }

    static (List<(RuleRef rr, Int32 index, String cond)> withKeyword, List<(RuleRef rr, Int32 index)> ambiguous) ClassifyAlternatives(
        List<(RuleRef RuleRef, Alternative Alt)> alternatives, EmitContext ctx)
    {
        var withKeyword = new List<(RuleRef, Int32, String)>();
        var ambiguous = new List<(RuleRef, Int32)>();
        var seenConditions = new HashSet<String>();

        for (var i = 0; i < alternatives.Count; i++)
        {
            var (rr, _) = alternatives[i];
            var firstToken = ctx.GetFirstToken(rr.Rule);

            // Only use keyword disambiguation for rules with a unique keyword prefix
            // (single-alternative sequence rules starting with a string literal)
            if (firstToken != null && firstToken.Length > 1 &&
                rr.Rule.Alternatives.Count == 1 &&
                rr.Rule.Alternatives[0].Items[0] is Literal<String>)
            {
                var cond = $"input.StartsWith(\"{EscapeString(firstToken)}\")";
                if (seenConditions.Add(cond))
                    withKeyword.Add((rr, i + 1, cond));
                else
                    ambiguous.Add((rr, i + 1));
            }
            else
            {
                ambiguous.Add((rr, i + 1));
            }
        }

        // Sort withKeyword: string keywords before char-class conditions
        withKeyword.Sort((a, b) =>
        {
            var aIsKeyword = a.Item3.Contains("StartsWith");
            var bIsKeyword = b.Item3.Contains("StartsWith");
            if (aIsKeyword && !bIsKeyword) return -1;
            if (!aIsKeyword && bIsKeyword) return 1;
            return 0;
        });

        // Sort ambiguous: more-specific rules first (more items in their sequence)
        ambiguous.Sort((a, b) =>
        {
            var aItems = a.Item1.Rule.Alternatives.Count > 0 ? a.Item1.Rule.Alternatives[0].Items.Count : 0;
            var bItems = b.Item1.Rule.Alternatives.Count > 0 ? b.Item1.Rule.Alternatives[0].Items.Count : 0;
            return bItems.CompareTo(aItems);
        });

        // If any alternative has a repetition quantifier (+ / *), move all alternatives
        // with the same or overlapping first-set to the ambiguous group, and try the
        // repetition alternative first (it's more specific / greedy).
        var hasRepetition = alternatives.Any(a => a.RuleRef.Quantifier.Max == null);
        if (hasRepetition)
        {
            // Move all withKeyword entries to ambiguous — disambiguation must be try/catch
            foreach (var (rr, idx, _) in withKeyword)
                ambiguous.Add((rr, idx));
            withKeyword.Clear();

            // Sort: repetition alternatives first
            ambiguous.Sort((a, b) =>
            {
                var aRep = a.Item1.Quantifier.Max == null;
                var bRep = b.Item1.Quantifier.Max == null;
                if (aRep && !bRep) return -1;
                if (!aRep && bRep) return 1;
                return 0;
            });
        }

        return (withKeyword, ambiguous);
    }


    static void EmitAlternativeBranch(String condition, RuleRef rr, Int32 index, Boolean isFirst, EmitContext ctx)
    {
        var w = ctx.W;
        if (isFirst)
            w.Line($"if ({condition})");
        else
            w.Line($"else if ({condition})");
        w.Line("{");
        w.PushIndent();
        EmitAlternativeConstruction(rr, index, ctx);
        w.PopIndent();
        w.Line("}");
    }

    static void EmitAmbiguousTryCatchChain(List<(RuleRef rr, Int32 index)> ambiguous, Int32 startPos, EmitContext ctx)
    {
        var w = ctx.W;
        for (var pos = startPos; pos < ambiguous.Count; pos++)
        {
            var (rr, index) = ambiguous[pos];
            var isLast = pos == ambiguous.Count - 1;
            EmitAlternativeConstruction(rr, index, ctx, checkLength: !isLast);
        }
    }

    static void EmitAlternativeConstruction(RuleRef rr, Int32 index, EmitContext ctx, Boolean checkLength = false)
    {
        var w = ctx.W;
        var q = rr.Quantifier;

        if (q is { Min: 1, Max: 1 })
        {
            w.Line($"var {CamelCase(rr.Name)} = new {rr.Name}(input);");
            w.Line($"index = {index};");
            w.Line($"Length = {CamelCase(rr.Name)}.Length;");
            if (checkLength) w.Line("if (Length >= 0) return;");
        }
        else if (q.Max == null)
        {
            w.Line($"var first = new {rr.Name}(input);");
            w.Line($"if (first.Length < 0) {{ Length = -1; {(checkLength ? "return;" : "return;")} }}");
            w.Line("var pos = first.Length;");
            EmitSkipWs(ctx);
            w.Line("while (pos < input.Length)");
            w.Line("{");
            w.PushIndent();
            w.Line($"var next = new {rr.Name}(input[pos..]);");
            w.Line("if (next.Length < 0) break;");
            w.Line("pos += next.Length;");
            EmitSkipWs(ctx);
            w.PopIndent();
            w.Line("}");
            w.Line($"index = {index};");
            w.Line("Length = pos;");
            if (checkLength) w.Line("if (Length >= 0) return;");
        }
    }

    static void EmitAlternativeVisitChildren(Rule rule, List<(RuleRef RuleRef, Alternative Alt)> alternatives, EmitContext ctx)
    {
        var w = ctx.W;
        var ruleId = ctx.GetRuleId(rule);
        w.Line($"public void VisitChildren<T>(ref T visitor) where T : {ctx.Namespace}.IVisitor, allows ref struct");
        w.Line("{");
        w.PushIndent();

        var hasRepetition = alternatives.Any(x => x.RuleRef.Quantifier.Max == null);

        if (hasRepetition)
        {
            for (var i = 0; i < alternatives.Count; i++)
            {
                var (rr, _) = alternatives[i];
                var index = i + 1;
                var keyword = i > 0 ? "else if" : "if";
                w.Line($"{keyword} (index == {index})");
                w.Line("{");
                w.PushIndent();
                if (rr.Quantifier.Max == null)
                {
                    w.Line($"foreach (var elem in new {rr.Name}Enumerable(input[..Length]))");
                    w.PushIndent();
                    w.Line("visitor.Visit(elem);");
                    w.PopIndent();
                }
                else
                {
                    w.Line($"visitor.Visit(new {rr.Name}(input, input, {ruleId}));");
                }
                w.PopIndent();
                w.Line("}");
            }
        }
        else
        {
            w.Line("switch (index)");
            w.Line("{");
            w.PushIndent();
            for (var i = 0; i < alternatives.Count; i++)
            {
                var (rr, _) = alternatives[i];
                w.Line($"case {i + 1}: visitor.Visit(new {rr.Name}(input, input, {ruleId})); break;");
            }
            w.PopIndent();
            w.Line("}");
        }

        w.PopIndent();
        w.Line("}");
    }

    static void EmitAlternativeVisitMethod(Rule rule, List<(RuleRef RuleRef, Alternative Alt)> alternatives, EmitContext ctx)
    {
        var w = ctx.W;
        w.Line("public void Visit<T>(T visitor) where T : IVisitor");
        w.Line("{");
        w.PushIndent();
        w.Line("switch (index)");
        w.Line("{");
        w.PushIndent();
        w.Line("case 0: throw new UninitializedInstanceException();");
        for (var i = 0; i < alternatives.Count; i++)
        {
            var (rr, _) = alternatives[i];
            if (rr.Quantifier.Max == null)
                w.Line($"case {i + 1}: visitor.Visit(new {rr.Name}Enumerable(input[..Length])); break;");
            else
                w.Line($"case {i + 1}: visitor.Visit(new {rr.Name}(input)); break;");
        }
        w.Line("case Byte.MaxValue: visitor.Visit(new ParseError()); break;");
        w.Line("default: throw new ArgumentOutOfRangeException(nameof(index));");
        w.PopIndent();
        w.Line("}");
        w.PopIndent();
        w.Line("}");
    }

    static void EmitAlternativeIVisitor(Rule rule, List<(RuleRef RuleRef, Alternative Alt)> alternatives, EmitContext ctx)
    {
        var w = ctx.W;
        w.Line("public interface IVisitor");
        w.Line("{");
        w.PushIndent();
        w.Line("void Visit(in ParseError parseError);");
        foreach (var (rr, _) in alternatives)
        {
            if (rr.Quantifier.Max == null)
                w.Line($"void Visit(in {rr.Name}Enumerable {CamelCase(rr.Name)}s);");
            else
                w.Line($"void Visit(in {rr.Name} {CamelCase(rr.Name)});");
        }
        w.PopIndent();
        w.Line("}");
    }

    // --- Repetition enumerables ---

    static void EmitRepetitionEnumerable(RuleRef rr, EmitContext ctx)
    {
        var w = ctx.W;
        var name = rr.Name;
        var wsSetup = ctx.PreserveWhitespace ? "" : "\n                    var ws = SkipWhitespace(remaining);\n                    remaining = remaining[ws..];";
        var consumedExpr = ctx.PreserveWhitespace ? "Current.Length" : "ws + Current.Length";

        w.Lines($$"""
            // {{name}}{{QuantifierSuffix(rr.Quantifier)}} enumerable
            public readonly ref struct {{name}}Enumerable
            {
                private readonly Input input;

                public {{name}}Enumerable(Input input) => this.input = input;

                public Enumerator GetEnumerator() => new(input);

                public ref struct Enumerator
                {
                    private readonly Input input;
                    private Int32 consumed;

                    public Enumerator(Input input) => this.input = input;

                    public {{name}} Current { get; private set; }

                    public Boolean MoveNext()
                    {
                        var remaining = input[consumed..];{{wsSetup}}
                        if (remaining.IsEmpty)
                            return false;
                        Current = new {{name}}(remaining);
                        if (Current.Length < 0)
                            return false;
                        consumed += {{consumedExpr}};
                        return true;
                    }
            """);
        w.PushIndent(); w.PushIndent();
        EmitSkipWhitespace(ctx);
        w.PopIndent(); w.PopIndent();
        w.Lines("""
                }
            }
            """);
    }

    // --- Epilogue ---

    static void EmitEpilogue(EmitContext ctx)
    {
        ctx.W.Blank();
        ctx.W.Lines("""
            // General-purpose tree printer
            public struct TreePrinter(Int32 depth = 0) : IVisitor
            {
                public void Visit<T>(in T rule) where T : IRule, allows ref struct
                {
                    var indent = new String(' ', depth * 2);
                    var checker = new ChildChecker();
                    rule.VisitChildren(ref checker);

                    if (checker.HasChildren)
                    {
                        Console.WriteLine($"{indent}{typeof(T).Name}");
                        var childPrinter = new TreePrinter(depth + 1);
                        rule.VisitChildren(ref childPrinter);
                    }
                    else
                    {
                        Console.WriteLine($"{indent}{typeof(T).Name}: {rule.Text}");
                    }
                }
            }

            public struct ChildChecker : IVisitor
            {
                public Boolean HasChildren { get; private set; }

                public void Visit<T>(in T rule) where T : IRule, allows ref struct
                {
                    HasChildren = true;
                }
            }

            public struct AncestorPrinter(List<String> ancestors, Int32 depth) : IVisitor
            {
                public AncestorPrinter() : this(new List<String>(), 0) { }

                public void Visit<T>(in T rule) where T : IRule, allows ref struct
                {
                    var name = typeof(T).Name;
                    ancestors.Add(name);

                    var indent = new String(' ', depth * 2);
                    var checker = new ChildChecker();
                    rule.VisitChildren(ref checker);

                    if (checker.HasChildren)
                    {
                        Console.WriteLine($"{indent}{name}  [{String.Join(" > ", ancestors)}]");
                        var child = new AncestorPrinter(ancestors, depth + 1);
                        rule.VisitChildren(ref child);
                    }
                    else
                    {
                        Console.WriteLine($"{indent}{name}: {rule.Text}  [{String.Join(" > ", ancestors)}]");
                    }

                    ancestors.RemoveAt(ancestors.Count - 1);
                }
            }
            """);
    }

    // --- Parent tracking ---

    static void EmitParentFields(EmitContext ctx)
    {
        ctx.W.Lines("""
            private readonly Input parentInput;
            private readonly Byte parentKind;
            """);
    }

    static void EmitParentAssignment(EmitContext ctx)
    {
        ctx.W.Lines("""
            this.parentInput = parentInput;
            this.parentKind = parentKind;
            """);
    }

    static void EmitVisitParent(EmitContext ctx, Boolean qualifyIVisitor = false)
    {
        var constraint = qualifyIVisitor ? $"{ctx.Namespace}.IVisitor" : "IVisitor";
        ctx.W.Lines($$"""
            public void VisitParent<T>(ref T visitor) where T : {{constraint}}, allows ref struct
            {
                Rules.Visit(parentKind, parentInput, ref visitor);
            }
            """);
    }

    static void EmitRulesDispatchClass(EmitContext ctx)
    {
        var w = ctx.W;
        w.Blank();
        w.Lines("""
            public static class Rules
            {
                public static void Visit<T>(Byte kind, Input input, ref T visitor) where T : IVisitor, allows ref struct
                {
                    switch (kind)
                    {
                        case 0: break;
            """);
        w.PushIndent(); w.PushIndent(); w.PushIndent();
        foreach (var rule in ctx.SortedRules)
        {
            var id = ctx.GetRuleId(rule);
            w.Line($"case {id}: visitor.Visit(new {rule.Name}(input)); break;");
        }
        w.PopIndent(); w.PopIndent(); w.PopIndent();
        w.Lines("""
                    }
                }
            }
            """);
    }

    static String ConstructorParams() => "Input input, Input parentInput = default, Byte parentKind = 0";

    // --- Shared helpers ---

    static void EmitSkipWhitespace(EmitContext ctx)
    {
        if (ctx.PreserveWhitespace)
            return;
        ctx.W.Lines("""
            private static Int32 SkipWhitespace(Input input)
            {
                var i = 0;
                while (i < input.Length && Char.IsWhiteSpace(input[i]))
                    i++;
                return i;
            }
            """);
    }

    static void EmitSkipWs(EmitContext ctx)
    {
        if (!ctx.PreserveWhitespace)
            ctx.W.Line("pos += SkipWhitespace(input[pos..]);");
    }

    static List<String> AssignPropertyNames(List<RuleRef> nonTerminals)
    {
        var names = new List<String>();
        var counts = new Dictionary<String, Int32>();
        foreach (var rr in nonTerminals)
        {
            var baseName = rr.Name;
            if (!counts.TryGetValue(baseName, out var count))
            {
                counts[baseName] = 1;
                names.Add(baseName);
            }
            else
            {
                counts[baseName] = count + 1;
                if (count == 1)
                {
                    var firstIdx = names.IndexOf(baseName);
                    names[firstIdx] = "Left";
                    names.Add("Right");
                }
                else
                {
                    names.Add($"{baseName}{count + 1}");
                }
            }
        }
        return names;
    }

    static readonly HashSet<String> CSharpKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum", "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    static String CamelCase(String name)
    {
        var result = Char.ToLowerInvariant(name[0]) + name[1..];
        return CSharpKeywords.Contains(result) ? "@" + result : result;
    }

    static String EscapeChar(Char c) => c switch
    {
        '\'' => "\\'",
        '\\' => "\\\\",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        '\0' => "\\0",
        '"' => "\\\"",
        _ => c.ToString()
    };

    static String EscapeString(String s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(EscapeChar(c));
        return sb.ToString();
    }

    // --- IndentedWriter ---

    sealed class IndentedWriter
    {
        readonly StringBuilder sb = new();
        Int32 indent;

        public void PushIndent() => indent++;
        public void PopIndent() => indent--;

        public void Line(String text)
        {
            if (text.Length > 0)
            {
                sb.Append(' ', indent * 4);
                sb.AppendLine(text);
            }
            else
            {
                sb.AppendLine();
            }
        }

        public void Lines(String text)
        {
            var lines = text.Split('\n');
            var minIndent = lines
                .Where(l => l.TrimStart().Length > 0)
                .Select(l => l.Length - l.TrimStart().Length)
                .DefaultIfEmpty(0)
                .Min();
            foreach (var line in lines)
            {
                var trimmed = line.Length >= minIndent ? line[minIndent..] : line.TrimStart();
                if (trimmed.Length == 0)
                    sb.AppendLine();
                else
                {
                    sb.Append(' ', indent * 4);
                    sb.AppendLine(trimmed);
                }
            }
        }

        public void Blank() => sb.AppendLine();

        public override String ToString() => sb.ToString();
    }
}

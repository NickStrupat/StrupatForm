using System.Text;

namespace StrupatForm;

public static class SfGrammarBuilder
{
    public static Grammar Parse(String text)
    {
        ReadOnlySpan<Char> input = text;
        var grammarNode = new SfParser.Grammar(input);
        var grammar = new Grammar();

        var ruleCollector = new RuleCollector(grammar);
        grammarNode.VisitChildren(ref ruleCollector);

        var ruleBuilder = new RuleBuilder(grammar);
        grammarNode.VisitChildren(ref ruleBuilder);

        return grammar;
    }

    struct RuleCollector(Grammar grammar) : SfParser.IVisitor
    {
        public void Visit<T>(in T rule) where T : SfParser.IRule, allows ref struct
        {
            if (typeof(T) == typeof(SfParser.Rule))
            {
                var r = new SfParser.Rule(rule.Text);
                var name = new String(r.Name.Text);
                if (!grammar.Rules.TryAdd(name, new Rule { Name = name }))
                    throw new Exception($"Rule `{name}` defined multiple times");
            }
        }
    }

    struct RuleBuilder(Grammar grammar) : SfParser.IVisitor
    {
        public void Visit<T>(in T rule) where T : SfParser.IRule, allows ref struct
        {
            if (typeof(T) == typeof(SfParser.Rule))
            {
                var r = new SfParser.Rule(rule.Text);
                var name = new String(r.Name.Text);
                var astRule = grammar.Rules[name];

                var alts = r.Alternatives;
                var altVisitor = new AlternativesVisitor(grammar, astRule);
                alts.VisitChildren(ref altVisitor);
            }
        }
    }

    struct AlternativesVisitor(Grammar grammar, Rule astRule) : SfParser.IVisitor
    {
        public void Visit<T>(in T rule) where T : SfParser.IRule, allows ref struct
        {
            if (typeof(T) == typeof(SfParser.InlineAlternatives))
            {
                var inline = new SfParser.InlineAlternatives(rule.Text);
                astRule.Alternatives.Add(BuildAlternative(inline.Alternative, grammar));
            }
            else if (typeof(T) == typeof(SfParser.IndentedAlternatives))
            {
                var indented = new SfParser.IndentedAlternatives(rule.Text);
                var collector = new IndentedAlternativeCollector(grammar, astRule);
                indented.VisitChildren(ref collector);
            }
        }
    }

    struct IndentedAlternativeCollector(Grammar grammar, Rule astRule) : SfParser.IVisitor
    {
        public void Visit<T>(in T rule) where T : SfParser.IRule, allows ref struct
        {
            if (typeof(T) == typeof(SfParser.IndentedAlternative))
            {
                var ia = new SfParser.IndentedAlternative(rule.Text);
                astRule.Alternatives.Add(BuildAlternative(ia.Alternative, grammar));
            }
        }
    }

    static Alternative BuildAlternative(SfParser.Alternative alt, Grammar grammar, Quantifier? quantifier = null)
    {
        var astAlt = new Alternative { Quantifier = quantifier ?? new() { Min = 1, Max = 1 } };
        var itemCollector = new ItemCollector(grammar, astAlt);
        alt.VisitChildren(ref itemCollector);
        return astAlt;
    }

    struct ItemCollector(Grammar grammar, Alternative astAlt) : SfParser.IVisitor
    {
        public void Visit<T>(in T rule) where T : SfParser.IRule, allows ref struct
        {
            if (typeof(T) == typeof(SfParser.Item))
            {
                var item = new SfParser.Item(rule.Text);
                var quantifier = BuildQuantifier(item);
                var atom = item.Atom;
                var atomVisitor = new AtomVisitor(grammar, astAlt, quantifier);
                atom.Visit(atomVisitor);
            }
        }
    }

    sealed class AtomVisitor(Grammar grammar, Alternative astAlt, Quantifier quantifier) : SfParser.Atom.IVisitor
    {
        public void Visit(in SfParser.ParseError parseError) { }

        public void Visit(in SfParser.Group group)
        {
            var subAlt = BuildAlternative(group.Alternative, grammar, quantifier);
            astAlt.Items.Add(subAlt);
        }

        public void Visit(in SfParser.RuleRef ruleRef)
        {
            var name = new String(ruleRef.Name.Text);
            var rule = grammar.Rules[name];
            astAlt.Items.Add(new RuleRef { Name = name, Rule = rule, Quantifier = quantifier });
        }

        public void Visit(in SfParser.StringLiteral stringLiteral)
        {
            var raw = new String(stringLiteral.Text);
            var content = raw[1..^1];
            var unescaped = Unescape(content);
            astAlt.Items.Add(new Literal<String> { Value = unescaped, Quantifier = quantifier });
        }

        public void Visit(in SfParser.CharLiteral charLiteral)
        {
            var raw = new String(charLiteral.Text);
            var content = raw[1..^1];
            var unescaped = Unescape(content);
            astAlt.Items.Add(new Literal<String> { Value = unescaped, Quantifier = quantifier });
        }

        public void Visit(in SfParser.Class @class)
        {
            var text = new String(@class.Text);
            var astClass = BuildClass(text, quantifier);
            astAlt.Items.Add(astClass);
        }
    }

    static Quantifier BuildQuantifier(SfParser.Item item)
    {
        try
        {
            var q = new SfParser.Quantifier(item.Text.Slice(item.Atom.Length));
            var qText = new String(q.Text);
            return qText[0] switch
            {
                '?' => new Quantifier { Min = 0, Max = 1 },
                '*' => new Quantifier { Min = 0, Max = null },
                '+' => new Quantifier { Min = 1, Max = null },
                '{' => ParseBraceQuantifier(qText),
                _ => new Quantifier { Min = 1, Max = 1 }
            };
        }
        catch (SfParser.ParseException)
        {
            return new Quantifier { Min = 1, Max = 1 };
        }
    }

    static Quantifier ParseBraceQuantifier(String text)
    {
        var inner = text[1..^1];
        var parts = inner.Split(',');
        if (parts.Length == 1)
        {
            var n = UInt32.Parse(parts[0]);
            return new Quantifier { Min = n, Max = n };
        }
        var min = UInt32.Parse(parts[0]);
        if (String.IsNullOrEmpty(parts[1]))
            return new Quantifier { Min = min, Max = null };
        var max = UInt32.Parse(parts[1]);
        return new Quantifier { Min = min, Max = max };
    }

    static Class BuildClass(String text, Quantifier quantifier)
    {
        var i = 1;
        var negated = false;
        if (i < text.Length && text[i] == '^')
        {
            negated = true;
            i++;
        }

        var astClass = new Class { Negated = negated, Quantifier = quantifier };

        while (i < text.Length && text[i] != ']')
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                var next = text[i + 1];
                if (next is 'w' or 'W' or 'd' or 'D' or 's' or 'S')
                {
                    astClass.Ranges.Add(new RegexCharacterRange { Pattern = $"\\{next}" });
                    i += 2;
                    continue;
                }

                if (next == 'p' && i + 2 < text.Length && text[i + 2] == '{')
                {
                    var closeBrace = text.IndexOf('}', i + 3);
                    if (closeBrace >= 0)
                    {
                        var property = text[(i + 3)..closeBrace];
                        astClass.Ranges.Add(new RegexCharacterRange { Pattern = $"\\p{{{property}}}" });
                        i = closeBrace + 1;
                        continue;
                    }
                }

                var escaped = UnescapeChar(text[i + 1]);
                i += 2;

                if (i < text.Length && text[i] == '-' && i + 1 < text.Length && text[i + 1] != ']')
                {
                    i++;
                    Char to;
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        to = UnescapeChar(text[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        to = text[i];
                        i++;
                    }
                    astClass.Ranges.Add(new CharacterRange { From = (Rune)escaped, To = (Rune)to });
                }
                else
                {
                    astClass.Ranges.Add(new CharacterRange { From = (Rune)escaped, To = (Rune)escaped });
                }
            }
            else
            {
                var ch = text[i];
                i++;

                if (i < text.Length && text[i] == '-' && i + 1 < text.Length && text[i + 1] != ']')
                {
                    i++;
                    Char to;
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        to = UnescapeChar(text[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        to = text[i];
                        i++;
                    }
                    astClass.Ranges.Add(new CharacterRange { From = (Rune)ch, To = (Rune)to });
                }
                else
                {
                    astClass.Ranges.Add(new CharacterRange { From = (Rune)ch, To = (Rune)ch });
                }
            }
        }

        return astClass;
    }

    public static String Unescape(String text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                var next = text[i + 1];
                if (next == 'u' && i + 5 < text.Length)
                {
                    var hex = text.Substring(i + 2, 4);
                    sb.Append((Char)Convert.ToInt32(hex, 16));
                    i += 5;
                }
                else if (next == 'U' && i + 9 < text.Length)
                {
                    var hex = text.Substring(i + 2, 8);
                    sb.Append(Char.ConvertFromUtf32(Convert.ToInt32(hex, 16)));
                    i += 9;
                }
                else if (next == 'x' && i + 2 < text.Length && text[i + 2] == '{')
                {
                    var closeBrace = text.IndexOf('}', i + 3);
                    if (closeBrace >= 0)
                    {
                        var hex = text[(i + 3)..closeBrace];
                        sb.Append(Char.ConvertFromUtf32(Convert.ToInt32(hex, 16)));
                        i = closeBrace;
                    }
                }
                else
                {
                    sb.Append(UnescapeChar(next));
                    i++;
                }
            }
            else
            {
                sb.Append(text[i]);
            }
        }
        return sb.ToString();
    }

    static Char UnescapeChar(Char c) => c switch
    {
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        '0' => '\0',
        '\\' => '\\',
        '"' => '"',
        '\'' => '\'',
        _ => c
    };
}

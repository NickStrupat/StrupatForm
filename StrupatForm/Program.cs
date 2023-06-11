using Antlr4.Runtime;
using StrupatForm;

const String path = "grammar.sf";
var text = File.ReadAllText(path);
var stream = new CodePointCharStream(text) {name = path};
var lexer = new StrupatFormLexer(stream);
var tokens = new CommonTokenStream(lexer);
var parser = new StrupatFormParser(tokens);
var grammarCtx = parser.grammar_();
var grammar = grammarCtx.ToGrammar();
;

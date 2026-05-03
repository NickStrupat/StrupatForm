using Antlr4.Runtime;
using StrupatForm;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: StrupatForm <grammar.sf> [namespace]");
    return 1;
}

var path = args[0];
var namespaceName = args.Length > 1 ? args[1] : Path.GetFileNameWithoutExtension(path);

var text = File.ReadAllText(path);
var stream = new CodePointCharStream(text) { name = path };
var lexer = new StrupatFormLexer(stream);
var tokens = new CommonTokenStream(lexer);
var parser = new StrupatFormParser(tokens);
var grammarCtx = parser.grammar_();
var grammar = grammarCtx.ToGrammar();

Console.Write(CodeEmitter.Emit(grammar, namespaceName));
return 0;

using Antlr4.Runtime;
using GeneratedParser;
using StrupatForm;

var sample = """
function main {
    var x = 42;
    var name = "hello";
    x = x + 1;
    return x;
}
""";

const String path = "grammar.sf";
var text = File.ReadAllText(path);
var stream = new CodePointCharStream(text) {name = path};
var lexer = new StrupatFormLexer(stream);
var tokens = new CommonTokenStream(lexer);
var parser = new StrupatFormParser(tokens);
var grammarCtx = parser.grammar_();
var grammar = grammarCtx.ToGrammar();

var emitted = CodeEmitter.Emit(grammar, "GeneratedParser");
File.WriteAllText("GeneratedParser.cs", emitted);
Console.WriteLine("Generated parser written to GeneratedParser.cs");
Console.WriteLine($"({emitted.Split('\n').Length} lines)");

Console.WriteLine("\n--- Parse tree ---");
var cu = new CompilationUnit(sample);
var treePrinter = new TreePrinter();
treePrinter.Visit(cu);

Console.WriteLine("\n--- Ancestor view ---");
var cu2 = new CompilationUnit(sample);
var ancestorPrinter = new AncestorPrinter();
ancestorPrinter.Visit(cu2);

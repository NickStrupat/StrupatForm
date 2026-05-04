using StrupatForm;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: StrupatForm <grammar.sf> [namespace] [--preserve-whitespace]");
    return 1;
}

var path = args[0];
var namespaceName = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : Path.GetFileNameWithoutExtension(path);
var preserveWhitespace = args.Any(a => a == "--preserve-whitespace");

var text = File.ReadAllText(path);
var grammar = SfGrammarBuilder.Parse(text);

Console.Write(CodeEmitter.Emit(grammar, namespaceName, preserveWhitespace));
return 0;

namespace Interpreter.Ast.Patterns;

public sealed record BindPattern(string Name, Pattern Pattern) : Pattern;

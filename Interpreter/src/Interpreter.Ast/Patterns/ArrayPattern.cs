namespace Interpreter.Ast.Patterns;

public sealed record ArrayPattern(IReadOnlyList<Pattern> Elements) : Pattern;

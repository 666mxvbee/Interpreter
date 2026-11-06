namespace Interpreter.Ast.Patterns;

public sealed record SExpressionPattern(string Tag, IReadOnlyList<Pattern> Arguments) : Pattern;

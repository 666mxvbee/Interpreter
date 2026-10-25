namespace Interpreter.Ast.Expressions;

public sealed record SExpression(string Name, IReadOnlyList<Expression> Arguments) : Expression;

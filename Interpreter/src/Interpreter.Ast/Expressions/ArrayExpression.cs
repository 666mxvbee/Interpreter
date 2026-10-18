namespace Interpreter.Ast.Expressions;

public sealed record ArrayExpression(IReadOnlyList<Expression> Elements) : Expression;

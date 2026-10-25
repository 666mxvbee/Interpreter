namespace Interpreter.Ast.Expressions;

public sealed record SExpressionExpression(string Tag, IReadOnlyList<Expression> Arguments) : Expression;

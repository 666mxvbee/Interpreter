namespace Interpreter.Ast.Expressions;

public sealed record ElementExpression(
    Expression Source,
    Expression Index) : Expression;

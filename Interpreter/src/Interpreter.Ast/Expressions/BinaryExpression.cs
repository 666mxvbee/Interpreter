namespace Interpreter.Ast.Expressions;

public sealed record BinaryExpression(
    BinaryOperator Operator,
    Expression Left,
    Expression Right) : Expression;
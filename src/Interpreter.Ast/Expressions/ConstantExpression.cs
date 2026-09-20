namespace Interpreter.Ast.Expressions;

public sealed record ConstantExpression(int Value) : Expression;
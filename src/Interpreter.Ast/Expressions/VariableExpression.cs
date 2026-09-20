namespace Interpreter.Ast.Expressions;

public sealed record VariableExpression(string Name) : Expression;
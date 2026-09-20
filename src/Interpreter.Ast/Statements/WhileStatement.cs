using Interpreter.Ast.Expressions;

namespace Interpreter.Ast.Statements;

public sealed record WhileStatement(
    Expression Condition,
    Statement Body) : Statement;
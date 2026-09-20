using Interpreter.Ast.Expressions;

namespace Interpreter.Ast.Statements;

public sealed record IfStatement(
    Expression Condition,
    Statement ThenBranch,
    Statement ElseBranch) : Statement;
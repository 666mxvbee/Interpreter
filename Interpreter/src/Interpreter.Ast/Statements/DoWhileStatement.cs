using Interpreter.Ast.Expressions;

namespace Interpreter.Ast.Statements;

public sealed record DoWhileStatement(
    Statement Body,
    Expression Condition) : Statement;
using Interpreter.Ast.Expressions;

namespace Interpreter.Ast.Statements;

public sealed record SetStatement(
    Expression Destination,
    Expression Index,
    Expression Value) : Statement;

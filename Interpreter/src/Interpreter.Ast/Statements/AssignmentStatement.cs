using Interpreter.Ast.Expressions;

namespace Interpreter.Ast.Statements;

public sealed record AssignmentStatement(
    string Destination,
    Expression Source) : Statement;
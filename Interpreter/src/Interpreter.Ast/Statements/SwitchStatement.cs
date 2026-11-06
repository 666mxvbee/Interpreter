using Interpreter.Ast.Expressions;

namespace Interpreter.Ast.Statements;

public sealed record SwitchStatement(
    Expression Scrutinee,
    IReadOnlyList<SwitchBranch> Branches) : Statement;

namespace Interpreter.Ast.Statements;

public sealed record SequenceStatement(
    Statement Left,
    Statement Right) : Statement;
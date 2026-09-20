namespace Interpreter.Ast.Statements;

public sealed record ReadStatement(string Destination) : Statement;
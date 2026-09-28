using Interpreter.Ast.Expressions;

namespace Interpreter.Ast.Statements;

public sealed record WriteStatement(Expression Value) : Statement;
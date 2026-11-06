using Interpreter.Ast.Patterns;

namespace Interpreter.Ast.Statements;

public sealed record SwitchBranch(Pattern Pattern, Statement Body);

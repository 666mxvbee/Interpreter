namespace StackMachine.Core.Instructions;

public sealed record SExpressionInstruction(string Name, int ArgumentCount) : Instruction;

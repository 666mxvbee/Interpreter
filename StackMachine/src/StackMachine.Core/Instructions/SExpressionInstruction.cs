namespace StackMachine.Core.Instructions;

public sealed record SExpressionInstruction(string Tag, int ArgumentCount) : Instruction;

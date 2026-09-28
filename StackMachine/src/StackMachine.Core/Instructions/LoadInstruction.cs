namespace StackMachine.Core.Instructions;

public sealed record LoadInstruction(string VariableName) : Instruction;
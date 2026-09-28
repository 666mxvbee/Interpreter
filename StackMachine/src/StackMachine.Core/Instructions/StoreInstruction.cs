namespace StackMachine.Core.Instructions;

public sealed record StoreInstruction(string VariableName) : Instruction;

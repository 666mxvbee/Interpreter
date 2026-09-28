namespace StackMachine.Core.Instructions;

public sealed record ConstantInstruction(int Value) : Instruction;
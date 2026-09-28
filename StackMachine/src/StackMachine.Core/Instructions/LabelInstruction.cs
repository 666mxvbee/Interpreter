namespace StackMachine.Core.Instructions;

public sealed record LabelInstruction(
    string Name) : Instruction;
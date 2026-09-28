namespace StackMachine.Core.Instructions;

public sealed record JumpIfNotZeroInstruction(
    string Target) : Instruction;
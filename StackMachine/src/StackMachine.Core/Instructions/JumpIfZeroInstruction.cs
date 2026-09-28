namespace StackMachine.Core.Instructions;

public sealed record JumpIfZeroInstruction(
    string Target) : Instruction;
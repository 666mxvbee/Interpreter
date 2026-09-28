namespace StackMachine.Core.Instructions;

public sealed record JumpInstruction(
    string Target) : Instruction;
namespace StackMachine.Core.Instructions;

public sealed record BinaryOperationInstruction(
    BinaryOperator Operator) : Instruction;
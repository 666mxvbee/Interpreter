namespace StackMachine.Core.Instructions;

public enum BinaryOperator
{
    LogicalOr,
    LogicalAnd,

    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,

    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder,
}
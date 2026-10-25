using Language.Runtime.Arithmetic;
using Language.Runtime.Values;
using StackMachine.Core.Instructions;

namespace StackMachine.Core.Execution;

internal static class BinaryOperatorEvaluator
{
    public static int Evaluate(BinaryOperator operation, int left, int right)
    {
        return Evaluate(operation, new IntegerValue(left), new IntegerValue(right)).AsInteger();
    }

    public static RuntimeValue Evaluate(BinaryOperator operation, RuntimeValue left, RuntimeValue right)
    {
        string symbol = operation switch
        {
            BinaryOperator.LogicalOr => "!!",
            BinaryOperator.LogicalAnd => "&&",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Remainder => "%",
            BinaryOperator.Cons => ":",
            _ => throw new MachineExecutionException($"Unsupported binary operator {operation}"),
        };

        return BinaryOperations.Evaluate(symbol, left, right);
    }
}

using StackMachine.Core.Instructions;

namespace StackMachine.Core.Execution;

internal static class BinaryOperatorEvaluator
{
    public static int Evaluate(
        BinaryOperator operation,
        int left,
        int right)
    {
        return operation switch
        {
            BinaryOperator.LogicalOr =>
                ToInteger(left != 0 || right != 0),

            BinaryOperator.LogicalAnd =>
                ToInteger(left != 0 && right != 0),

            BinaryOperator.Equal =>
                ToInteger(left == right),

            BinaryOperator.NotEqual =>
                ToInteger(left != right),

            BinaryOperator.LessThan =>
                ToInteger(left < right),

            BinaryOperator.LessThanOrEqual =>
                ToInteger(left <= right),

            BinaryOperator.GreaterThan =>
                ToInteger(left > right),

            BinaryOperator.GreaterThanOrEqual =>
                ToInteger(left >= right),

            BinaryOperator.Add =>
                unchecked(left + right),

            BinaryOperator.Subtract =>
                unchecked(left - right),

            BinaryOperator.Multiply =>
                unchecked(left * right),

            BinaryOperator.Divide =>
                Divide(left, right),

            BinaryOperator.Remainder =>
                Remainder(left, right),

            _ => throw new MachineExecutionException(
                $"Unsupported binary operator \"{operation}\"."),
        };
    }

    private static int Divide(int left, int right)
    {
        if (right == 0)
        {
            throw new MachineExecutionException(
                "Division by zero");
        }

        if (left == int.MinValue && right == -1)
        {
            return int.MinValue;
        }

        return left / right;
    }

    private static int Remainder(int left, int right)
    {
        if (right == 0)
        {
            throw new MachineExecutionException(
                "Division by zero");
        }

        if (left == int.MinValue && right == -1)
        {
            return 0;
        }

        return left % right;
    }

    private static int ToInteger(bool value)
    {
        return value ? 1 : 0;
    }
}
using Interpreter.Ast.Expressions;
using Interpreter.Runtime.Exceptions;
using Interpreter.Runtime.State;

namespace Interpreter.Runtime.Evaluation;

internal sealed class ExpressionEvaluator
{
    private readonly RuntimeState _state;

    public ExpressionEvaluator(RuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _state = state;
    }

    public int Evaluate(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            ConstantExpression constant => constant.Value,
            VariableExpression variable => _state.GetValue(variable.Name),
            BinaryExpression binary => EvaluateBinary(binary),

            _ => throw new InterpreterRuntimeException(
                $"Unsupported expression type {expression.GetType().Name}"),
        };
    }

    private static int ToInteger(bool value)
    {
        return value ? 1 : 0;
    }

    private static int Divide(int left, int right)
    {
        if (right == 0)
        {
            throw new InterpreterRuntimeException("Division by zero");
        }

        if (left == int.MinValue && right == 1)
        {
            return int.MinValue;
        }

        return left / right;
    }

    private static int Remainder(int left, int right)
    {
        if (right == 0)
        {
            throw new InterpreterRuntimeException("Division by zero");
        }

        if (left == int.MinValue && right == -1)
        {
            return 0;
        }

        return left % right;
    }

    private int EvaluateBinary(BinaryExpression expression)
    {
        int left = Evaluate(expression.Left);
        int right = Evaluate(expression.Right);

        return expression.Operator switch
        {
            BinaryOperator.LogicalOr => ToInteger(
                left != 0 || right != 0),

            BinaryOperator.LogicalAnd => ToInteger(
                left != 0 && right != 0),

            BinaryOperator.Equal => ToInteger(left == right),
            BinaryOperator.NotEqual => ToInteger(left != right),
            BinaryOperator.LessThan => ToInteger(left < right),
            BinaryOperator.LessThanOrEqual => ToInteger(left <= right),
            BinaryOperator.GreaterThan => ToInteger(left > right),
            BinaryOperator.GreaterThanOrEqual => ToInteger(left >= right),

            BinaryOperator.Add => unchecked(left + right),
            BinaryOperator.Subtract => unchecked(left - right),
            BinaryOperator.Multiply => unchecked(left * right),
            BinaryOperator.Divide => Divide(left, right),
            BinaryOperator.Remainder => Remainder(left, right),

            _ => throw new InterpreterRuntimeException(
                $"Unsupported binary operator {expression.Operator}"),
        };
    }
}
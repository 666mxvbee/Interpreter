using Interpreter.Ast.Expressions;
using Interpreter.Runtime.Exceptions;
using Interpreter.Runtime.State;
using Interpreter.Runtime.Values;

namespace Interpreter.Runtime.Evaluation;

internal sealed class ExpressionEvaluator
{
    private readonly RuntimeState _state;

    public ExpressionEvaluator(RuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _state = state;
    }

    public RuntimeValue Evaluate(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            ConstantExpression constant => new IntegerValue(constant.Value),
            StringExpression text => new StringValue(text.Value),
            ArrayExpression array => EvaluateArray(array),
            ElementExpression element => Evaluate(element.Source)
                .GetElement(Evaluate(element.Index).AsInteger()),
            LengthExpression length => new IntegerValue(Evaluate(length.Source).Length),
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
            throw new InterpreterRuntimeException("Division by zero");
        }

        if (left == int.MinValue && right == -1)
        {
            return 0;
        }

        return left % right;
    }

    private ArrayValue EvaluateArray(ArrayExpression expression)
    {
        var elements = new RuntimeValue[expression.Elements.Count];

        for (int index = 0; index < elements.Length; index++)
        {
            elements[index] = Evaluate(expression.Elements[index]);
        }

        return new ArrayValue(elements);
    }

    private IntegerValue EvaluateBinary(BinaryExpression expression)
    {
        RuntimeValue left = Evaluate(expression.Left);
        RuntimeValue right = Evaluate(expression.Right);

        int value = expression.Operator switch
        {
            BinaryOperator.LogicalOr => ToInteger(
                (left.AsInteger() != 0) | (right.AsInteger() != 0)),
            BinaryOperator.LogicalAnd => ToInteger(
                (left.AsInteger() != 0) & (right.AsInteger() != 0)),

            BinaryOperator.Equal => ToInteger(left.CompareValue(right) == 0),
            BinaryOperator.NotEqual => ToInteger(left.CompareValue(right) != 0),
            BinaryOperator.LessThan => ToInteger(left.CompareValue(right) < 0),
            BinaryOperator.LessThanOrEqual => ToInteger(left.CompareValue(right) <= 0),
            BinaryOperator.GreaterThan => ToInteger(left.CompareValue(right) > 0),
            BinaryOperator.GreaterThanOrEqual => ToInteger(left.CompareValue(right) >= 0),

            BinaryOperator.Add => unchecked(left.AsInteger() + right.AsInteger()),
            BinaryOperator.Subtract => unchecked(left.AsInteger() - right.AsInteger()),
            BinaryOperator.Multiply => unchecked(left.AsInteger() * right.AsInteger()),
            BinaryOperator.Divide => Divide(left.AsInteger(), right.AsInteger()),
            BinaryOperator.Remainder => Remainder(left.AsInteger(), right.AsInteger()),

            _ => throw new InterpreterRuntimeException(
                $"Unsupported binary operator {expression.Operator}"),
        };

        return new IntegerValue(value);
    }
}

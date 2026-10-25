using Interpreter.Ast.Expressions;
using Interpreter.Runtime.Exceptions;
using Interpreter.Runtime.State;
using Language.Runtime;
using Language.Runtime.Arithmetic;
using Language.Runtime.Values;

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
        var pending = new Stack<Evaluation>();
        var values = new Stack<RuntimeValue>();
        pending.Push(new Evaluation(expression, false, 0));

        while (pending.TryPop(out Evaluation item))
        {
            if (item.Depth > RuntimeLimits.MaximumNestingDepth)
            {
                throw new InterpreterRuntimeException("Expression nesting depth limit exceeded");
            }

            if (item.Expanded)
            {
                values.Push(Complete(item.Expression, values));
                continue;
            }

            switch (item.Expression)
            {
                case ConstantExpression constant:
                    values.Push(new IntegerValue(constant.Value));
                    continue;
                case StringExpression text:
                    values.Push(new StringValue(text.Value));
                    continue;
                case VariableExpression variable:
                    values.Push(_state.GetValue(variable.Name));
                    continue;
            }

            pending.Push(item with { Expanded = true });

            switch (item.Expression)
            {
                case BinaryExpression binary:
                    pending.Push(new Evaluation(binary.Left, false, item.Depth + 1));
                    pending.Push(new Evaluation(binary.Right, false, item.Depth + 1));
                    break;
                case ElementExpression element:
                    pending.Push(new Evaluation(element.Source, false, item.Depth + 1));
                    pending.Push(new Evaluation(element.Index, false, item.Depth + 1));
                    break;
                case LengthExpression length:
                    pending.Push(new Evaluation(length.Source, false, item.Depth + 1));
                    break;
                case ArrayExpression array:
                    PushElements(array.Elements, item.Depth, pending);
                    break;
                case SExpressionExpression constructor:
                    PushElements(constructor.Arguments, item.Depth, pending);
                    break;
                default:
                    throw new InterpreterRuntimeException(
                        $"Unsupported expression type {item.Expression.GetType().Name}");
            }
        }

        return values.Pop();
    }

    private static void PushElements(IReadOnlyList<Expression> elements, int depth, Stack<Evaluation> pending)
    {
        for (int index = elements.Count - 1; index >= 0; index--)
        {
            pending.Push(new Evaluation(elements[index], false, depth + 1));
        }
    }

    private static RuntimeValue Complete(Expression expression, Stack<RuntimeValue> values)
    {
        switch (expression)
        {
            case BinaryExpression binary:
                RuntimeValue left = values.Pop();
                RuntimeValue right = values.Pop();
                return BinaryOperations.Evaluate(GetSymbol(binary.Operator), left, right);
            case ElementExpression:
                RuntimeValue source = values.Pop();
                return source.GetElement(values.Pop().AsInteger());
            case LengthExpression:
                return new IntegerValue(values.Pop().GetLength());
            case ArrayExpression array:
                return new ArrayValue(PopElements(array.Elements.Count, values));
            case SExpressionExpression constructor:
                return new SExpressionValue(constructor.Tag, PopElements(constructor.Arguments.Count, values));
            default:
                throw new InterpreterRuntimeException($"Unsupported expression type {expression.GetType().Name}");
        }
    }

    private static RuntimeValue[] PopElements(int count, Stack<RuntimeValue> values)
    {
        var elements = new RuntimeValue[count];

        for (int index = count - 1; index >= 0; index--)
        {
            elements[index] = values.Pop();
        }

        return elements;
    }

    private static string GetSymbol(BinaryOperator operation)
    {
        return operation switch
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
            _ => throw new InterpreterRuntimeException($"Unsupported binary operator {operation}"),
        };
    }

    private readonly record struct Evaluation(Expression Expression, bool Expanded, int Depth);
}

using Language.Runtime.Exceptions;
using Language.Runtime.Values;
using System.Globalization;
using System.Text;

namespace Language.Runtime.Formatting;

public static class RuntimeValueFormatter
{
    public static string FormatOutput(IEnumerable<RuntimeValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return string.Join(", ", values.Select(Format));
    }

    public static string Format(RuntimeValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new StringBuilder();
        var pending = new Stack<WorkItem>();
        var active = new HashSet<RuntimeValue>(ReferenceEqualityComparer.Instance);
        pending.Push(new ValueWork(value, 0));

        while (pending.TryPop(out WorkItem? work))
        {
            switch (work)
            {
                case TextWork text:
                    result.Append(text.Text);
                    break;

                case ExitWork exit:
                    active.Remove(exit.Value);
                    break;

                case ListWork list:
                    FormatList(list, result, pending, active);
                    break;

                case ValueWork item:
                    FormatValue(item, result, pending, active);
                    break;
            }
        }

        return result.ToString();
    }

    private static void FormatValue(
        ValueWork item,
        StringBuilder result,
        Stack<WorkItem> pending,
        HashSet<RuntimeValue> active)
    {
        if (item.Depth > RuntimeLimits.MaximumNestingDepth)
        {
            throw new LanguageRuntimeException("Output nesting depth limit exceeded");
        }

        switch (item.Value)
        {
            case IntegerValue integer:
                result.Append(integer.Value.ToString(CultureInfo.InvariantCulture));
                break;

            case StringValue text:
                result.Append('"').Append(text.Text).Append('"');
                break;

            case SExpressionValue { Tag: "nil" } empty when empty.GetLength() == 0:
                result.Append("{}");
                break;

            case SExpressionValue { Tag: "cons" } list when list.GetLength() == 2:
                result.Append('{');
                pending.Push(new TextWork("}"));
                pending.Push(new ListWork(list, true, item.Depth));
                break;

            case SExpressionValue expression:
                Enter(expression, active);
                result.Append(expression.Tag);
                pending.Push(new ExitWork(expression));

                if (expression.GetLength() > 0)
                {
                    result.Append(" (");
                    pending.Push(new TextWork(")"));
                    PushElements(expression, item.Depth, pending);
                }

                break;

            case ArrayValue array:
                Enter(array, active);
                result.Append('[');
                pending.Push(new ExitWork(array));
                pending.Push(new TextWork("]"));
                PushElements(array, item.Depth, pending);
                break;

            default:
                throw new LanguageRuntimeException($"Unsupported value type {item.Value.GetType().Name}");
        }
    }

    private static void FormatList(
        ListWork item,
        StringBuilder result,
        Stack<WorkItem> pending,
        HashSet<RuntimeValue> active)
    {
        if (item.Value is SExpressionValue { Tag: "nil" } empty && empty.GetLength() == 0)
        {
            return;
        }

        if (item.Value is not SExpressionValue { Tag: "cons" } node || node.GetLength() != 2)
        {
            throw new LanguageRuntimeException("Cannot write an improper list");
        }

        Enter(node, active);

        if (!item.First)
        {
            result.Append(", ");
        }

        pending.Push(new ExitWork(node));
        pending.Push(new ListWork(node.GetElement(1), false, item.Depth));
        pending.Push(new ValueWork(node.GetElement(0), item.Depth + 1));
    }

    private static void PushElements(RuntimeValue value, int depth, Stack<WorkItem> pending)
    {
        for (int index = value.GetLength() - 1; index >= 0; index--)
        {
            pending.Push(new ValueWork(value.GetElement(index), depth + 1));

            if (index > 0)
            {
                pending.Push(new TextWork(", "));
            }
        }
    }

    private static void Enter(RuntimeValue value, HashSet<RuntimeValue> active)
    {
        if (!active.Add(value))
        {
            throw new LanguageRuntimeException("Cannot write a cyclic array or S-expression");
        }
    }

    private abstract record WorkItem;

    private sealed record ValueWork(RuntimeValue Value, int Depth) : WorkItem;

    private sealed record TextWork(string Text) : WorkItem;

    private sealed record ExitWork(RuntimeValue Value) : WorkItem;

    private sealed record ListWork(RuntimeValue Value, bool First, int Depth) : WorkItem;
}

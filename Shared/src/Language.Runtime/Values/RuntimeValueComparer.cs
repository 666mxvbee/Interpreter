using Language.Runtime.Exceptions;

namespace Language.Runtime.Values;

public static class RuntimeValueComparer
{
    public static int Compare(RuntimeValue left, RuntimeValue right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var pending = new Stack<Comparison>();
        var active = new HashSet<(RuntimeValue Left, RuntimeValue Right)>();
        pending.Push(new Comparison(left, right, 0, false));

        while (pending.TryPop(out Comparison item))
        {
            if (item.Exit)
            {
                active.Remove((item.Left, item.Right));
                continue;
            }

            if (item.Depth > RuntimeLimits.MaximumNestingDepth)
            {
                throw new LanguageRuntimeException("Comparison nesting depth limit exceeded");
            }

            int comparison;

            switch (item.Left, item.Right)
            {
                case (IntegerValue first, IntegerValue second):
                    comparison = first.Value.CompareTo(second.Value);
                    break;

                case (StringValue first, StringValue second):
                    comparison = first.CompareBytes(second);
                    break;

                case (ArrayValue first, ArrayValue second):
                    comparison = first.GetLength().CompareTo(second.GetLength());

                    if (comparison == 0)
                    {
                        if (!active.Add((first, second)))
                        {
                            throw new LanguageRuntimeException("Cannot compare cyclic arrays");
                        }

                        pending.Push(item with { Exit = true });

                        for (int index = first.GetLength() - 1; index >= 0; index--)
                        {
                            pending.Push(new Comparison(
                                first.GetElement(index), second.GetElement(index), item.Depth + 1, false));
                        }
                    }

                    break;

                default:
                    throw new LanguageRuntimeException(
                        "Comparison requires two integers, two strings, or two arrays with comparable elements");
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private readonly record struct Comparison(RuntimeValue Left, RuntimeValue Right, int Depth, bool Exit);
}

using Interpreter.Ast.Patterns;
using Interpreter.Runtime.Exceptions;
using Language.Runtime;
using Language.Runtime.Values;

namespace Interpreter.Runtime.Evaluation;

internal static class PatternMatcher
{
    public static IReadOnlyDictionary<string, RuntimeValue>? Match(Pattern pattern, RuntimeValue value)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(value);
        var bindings = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal);
        var pending = new Stack<MatchWork>();
        pending.Push(new MatchWork(pattern, value, false, 0));

        while (pending.TryPop(out MatchWork item))
        {
            if (item.Depth > RuntimeLimits.MaximumNestingDepth)
            {
                throw new InterpreterRuntimeException("Pattern nesting depth limit exceeded");
            }

            switch (item.Pattern)
            {
                case WildcardPattern:
                    break;
                case BindPattern bind:
                    if (item.Expanded)
                    {
                        bindings[bind.Name] = item.Value;
                    }
                    else
                    {
                        pending.Push(item with { Expanded = true });
                        pending.Push(new MatchWork(bind.Pattern, item.Value, false, item.Depth + 1));
                    }

                    break;
                case IntegerPattern integer:
                    if (item.Value is not IntegerValue number || number.Value != integer.Value)
                    {
                        return null;
                    }

                    break;
                case StringPattern text:
                    if (item.Value is not StringValue str || str.CompareBytes(new StringValue(text.Value)) != 0)
                    {
                        return null;
                    }

                    break;
                case TypePattern type:
                    if (!MatchesType(type.Type, item.Value))
                    {
                        return null;
                    }

                    break;
                case ArrayPattern array:
                    if (item.Value is not ArrayValue elements || elements.GetLength() != array.Elements.Count)
                    {
                        return null;
                    }

                    PushElements(array.Elements, elements, item.Depth, pending);
                    break;
                case SExpressionPattern expression:
                    if (item.Value is not SExpressionValue sexp || sexp.Tag != expression.Tag
                        || sexp.GetLength() != expression.Arguments.Count)
                    {
                        return null;
                    }

                    PushElements(expression.Arguments, sexp, item.Depth, pending);
                    break;
                default:
                    throw new InterpreterRuntimeException($"Unsupported pattern type {item.Pattern.GetType().Name}");
            }
        }

        return bindings;
    }

    private static void PushElements(
        IReadOnlyList<Pattern> patterns,
        RuntimeValue value,
        int depth,
        Stack<MatchWork> pending)
    {
        for (int index = patterns.Count - 1; index >= 0; index--)
        {
            pending.Push(new MatchWork(patterns[index], value.GetElement(index), false, depth + 1));
        }
    }

    private static bool MatchesType(PatternValueType type, RuntimeValue value)
    {
        return type switch
        {
            PatternValueType.Integer => value is IntegerValue,
            PatternValueType.String => value is StringValue,
            PatternValueType.Array => value is ArrayValue,
            PatternValueType.SExpression => value is SExpressionValue,
            _ => throw new InterpreterRuntimeException($"Unsupported pattern value type {type}"),
        };
    }

    private readonly record struct MatchWork(Pattern Pattern, RuntimeValue Value, bool Expanded, int Depth);
}

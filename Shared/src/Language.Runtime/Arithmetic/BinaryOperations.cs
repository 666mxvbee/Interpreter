using Language.Runtime.Exceptions;
using Language.Runtime.Values;

namespace Language.Runtime.Arithmetic;

public static class BinaryOperations
{
    public static RuntimeValue Evaluate(string operation, RuntimeValue left, RuntimeValue right)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (operation == ":")
        {
            return new SExpressionValue("cons", new[] { left, right });
        }

        if (operation is "==" or "!=" or "<" or "<=" or ">" or ">=")
        {
            int comparison = RuntimeValueComparer.Compare(left, right);
            bool result = operation switch
            {
                "==" => comparison == 0,
                "!=" => comparison != 0,
                "<" => comparison < 0,
                "<=" => comparison <= 0,
                ">" => comparison > 0,
                _ => comparison >= 0,
            };

            return new IntegerValue(result ? 1 : 0);
        }

        int first = left.AsInteger();
        int second = right.AsInteger();
        int value = operation switch
        {
            "+" => IntegerArithmetic.Add(first, second),
            "-" => IntegerArithmetic.Subtract(first, second),
            "*" => IntegerArithmetic.Multiply(first, second),
            "/" => IntegerArithmetic.Divide(first, second),
            "%" => IntegerArithmetic.Remainder(first, second),
            "&&" => first != 0 && second != 0 ? 1 : 0,
            "!!" => first != 0 || second != 0 ? 1 : 0,
            _ => throw new LanguageRuntimeException($"Unsupported binary operator \"{operation}\""),
        };

        return new IntegerValue(value);
    }
}

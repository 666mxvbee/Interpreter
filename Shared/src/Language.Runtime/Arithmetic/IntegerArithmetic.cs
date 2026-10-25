using Language.Runtime.Exceptions;

namespace Language.Runtime.Arithmetic;

public static class IntegerArithmetic
{
    public static int Add(int left, int right)
    {
        return unchecked(left + right);
    }

    public static int Subtract(int left, int right)
    {
        return unchecked(left - right);
    }

    public static int Multiply(int left, int right)
    {
        return unchecked(left * right);
    }

    public static int Divide(int left, int right)
    {
        if (right == 0)
        {
            throw new LanguageRuntimeException("Division by zero");
        }

        return left == int.MinValue && right == -1 ? int.MinValue : left / right;
    }

    public static int Remainder(int left, int right)
    {
        if (right == 0)
        {
            throw new LanguageRuntimeException("Division by zero");
        }

        return left == int.MinValue && right == -1 ? 0 : left % right;
    }
}

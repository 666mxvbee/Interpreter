using System.Globalization;

namespace StackMachine.Core.Values;

public sealed class IntegerValue : RuntimeValue
{
    public IntegerValue(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public override int AsInteger()
    {
        return Value;
    }

    public override int CompareValue(RuntimeValue other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Value.CompareTo(other.AsInteger());
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}

namespace Language.Runtime.Values;

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
}

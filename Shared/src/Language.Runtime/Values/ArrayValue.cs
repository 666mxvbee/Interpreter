namespace Language.Runtime.Values;

public sealed class ArrayValue : RuntimeValue
{
    private readonly RuntimeValue[] _elements;

    public ArrayValue(IEnumerable<RuntimeValue> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        _elements = elements.ToArray();

        if (_elements.Any(element => element is null))
        {
            throw new ArgumentException("Array elements cannot be null", nameof(elements));
        }
    }

    public override int Length => _elements.Length;

    public override RuntimeValue GetElement(int index)
    {
        ValidateIndex(index, Length);
        return _elements[index];
    }

    public override void SetElement(int index, RuntimeValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateIndex(index, Length);
        _elements[index] = value;
    }
}

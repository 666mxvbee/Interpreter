namespace Language.Runtime.Values;

public sealed class SExpressionValue : RuntimeValue
{
    private readonly ArrayValue _arguments;

    public SExpressionValue(string tag, IEnumerable<RuntimeValue> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        Tag = tag;
        _arguments = new ArrayValue(arguments);
    }

    public string Tag { get; }

    public static SExpressionValue FromList(IEnumerable<RuntimeValue> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        RuntimeValue[] items = elements.ToArray();
        var result = new SExpressionValue("nil", Array.Empty<RuntimeValue>());

        for (int index = items.Length - 1; index >= 0; index--)
        {
            result = new SExpressionValue("cons", new[] { items[index], result });
        }

        return result;
    }

    public override int GetLength()
    {
        return _arguments.GetLength();
    }

    public override RuntimeValue GetElement(int index)
    {
        return _arguments.GetElement(index);
    }

    public override void SetElement(int index, RuntimeValue value)
    {
        _arguments.SetElement(index, value);
    }
}

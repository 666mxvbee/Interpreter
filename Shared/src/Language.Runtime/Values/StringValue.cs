using Language.Runtime.Exceptions;
using System.Text;

namespace Language.Runtime.Values;

public sealed class StringValue : RuntimeValue
{
    private readonly byte[] _bytes;

    public StringValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _bytes = Encoding.UTF8.GetBytes(value);
    }

    public string Text => Encoding.UTF8.GetString(_bytes);

    public override int GetLength()
    {
        return _bytes.Length;
    }

    public override RuntimeValue GetElement(int index)
    {
        ValidateIndex(index, GetLength());
        return new IntegerValue(_bytes[index]);
    }

    public override void SetElement(int index, RuntimeValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateIndex(index, GetLength());
        int character = value.AsInteger();

        if (character is < byte.MinValue or > byte.MaxValue)
        {
            throw new LanguageRuntimeException("String byte must be between 0 and 255");
        }

        _bytes[index] = (byte)character;
    }

    public int CompareBytes(StringValue other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return _bytes.AsSpan().SequenceCompareTo(other._bytes);
    }
}

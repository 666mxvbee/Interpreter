using StackMachine.Core.Execution;
using System.Text;

namespace StackMachine.Core.Values;

public sealed class StringValue : RuntimeValue
{
    private readonly byte[] _bytes;

    public StringValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _bytes = Encoding.UTF8.GetBytes(value);
    }

    public override int Length => _bytes.Length;

    public override RuntimeValue GetElement(int index)
    {
        ValidateIndex(index, Length);

        return new IntegerValue(_bytes[index]);
    }

    public override void SetElement(int index, RuntimeValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        ValidateIndex(index, Length);

        int character = value.AsInteger();

        if (character is < byte.MinValue or > byte.MaxValue)
        {
            throw new MachineExecutionException("String byte must be between 0 and 255");
        }

        _bytes[index] = (byte)character;
    }

    public override int CompareValue(RuntimeValue other)
    {
        if (other is not StringValue text)
        {
            throw new MachineExecutionException("A string can only be compared with a string");
        }

        return _bytes.AsSpan().SequenceCompareTo(text._bytes);
    }

    public override string ToString()
    {
        return $"\"{Encoding.UTF8.GetString(_bytes)}\"";
    }
}

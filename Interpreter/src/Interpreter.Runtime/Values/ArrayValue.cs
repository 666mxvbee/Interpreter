using Interpreter.Runtime.Exceptions;

namespace Interpreter.Runtime.Values;

internal sealed class ArrayValue : RuntimeValue
{
    private readonly RuntimeValue[] _elements;

    public ArrayValue(RuntimeValue[] elements)
    {
        _elements = elements;
    }

    public override int Length => _elements.Length;

    public override RuntimeValue GetElement(int index)
    {
        ValidateIndex(index, Length);

        return _elements[index];
    }

    public override void SetElement(int index, RuntimeValue value)
    {
        ValidateIndex(index, Length);

        _elements[index] = value;
    }

    public override int CompareValue(RuntimeValue other)
    {
        if (other is not ArrayValue array)
        {
            throw new InterpreterRuntimeException("An array can only be compared with an array");
        }

        return CompareArray(array, new HashSet<(ArrayValue Left, ArrayValue Right)>());
    }

    public override string ToString()
    {
        return Format(new HashSet<ArrayValue>());
    }

    private int CompareArray(ArrayValue other, HashSet<(ArrayValue Left, ArrayValue Right)> active)
    {
        int lengthComparison = Length.CompareTo(other.Length);

        if (lengthComparison != 0)
        {
            return lengthComparison;
        }

        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (!active.Add((this, other)))
        {
            throw new InterpreterRuntimeException("Cannot compare cyclic arrays");
        }

        for (int index = 0; index < Length; index++)
        {
            RuntimeValue left = _elements[index];
            RuntimeValue right = other._elements[index];
            int comparison = left is ArrayValue leftArray && right is ArrayValue rightArray
                ? leftArray.CompareArray(rightArray, active)
                : left.CompareValue(right);

            if (comparison != 0)
            {
                return comparison;
            }
        }

        active.Remove((this, other));

        return 0;
    }

    private string Format(HashSet<ArrayValue> active)
    {
        if (!active.Add(this))
        {
            throw new InterpreterRuntimeException("Cannot write a cyclic array");
        }

        string[] elements = new string[Length];

        for (int index = 0; index < Length; index++)
        {
            RuntimeValue value = _elements[index];
            elements[index] = value is ArrayValue array
                ? array.Format(active)
                : value.ToString();
        }

        active.Remove(this);

        return $"[{string.Join("; ", elements)}]";
    }
}

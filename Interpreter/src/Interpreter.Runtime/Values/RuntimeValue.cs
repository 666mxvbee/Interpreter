using Interpreter.Runtime.Exceptions;

namespace Interpreter.Runtime.Values;

internal abstract class RuntimeValue
{
    public virtual int Length => throw new InterpreterRuntimeException(
        "Length requires an array or a string");

    public virtual int AsInteger()
    {
        throw new InterpreterRuntimeException("Expected an integer value");
    }

    public virtual RuntimeValue GetElement(int index)
    {
        throw new InterpreterRuntimeException("Indexing requires an array or a string");
    }

    public virtual void SetElement(int index, RuntimeValue value)
    {
        throw new InterpreterRuntimeException("Element assignment requires an array or a string");
    }

    public abstract int CompareValue(RuntimeValue other);

    public abstract override string ToString();

    protected static void ValidateIndex(int index, int length)
    {
        if (index < 0 || index >= length)
        {
            throw new InterpreterRuntimeException(
                $"Index {index} is out of bounds for length {length}");
        }
    }
}

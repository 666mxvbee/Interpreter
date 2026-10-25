using StackMachine.Core.Execution;

namespace StackMachine.Core.Values;

public abstract class RuntimeValue
{
    public virtual int Length => throw new MachineExecutionException(
        "Length requires an array, string, or S-expression");

    public virtual int AsInteger()
    {
        throw new MachineExecutionException("Expected an integer value");
    }

    public virtual RuntimeValue GetElement(int index)
    {
        throw new MachineExecutionException("Indexing requires an array, string, or S-expression");
    }

    public virtual void SetElement(int index, RuntimeValue value)
    {
        throw new MachineExecutionException("Element assignment requires an array, string, or S-expression");
    }

    public abstract int CompareValue(RuntimeValue other);

    public abstract override string ToString();

    internal virtual string Format(HashSet<RuntimeValue> active)
    {
        return ToString();
    }

    protected static void ValidateIndex(int index, int length)
    {
        if (index < 0 || index >= length)
        {
            throw new MachineExecutionException(
                $"Index {index} is out of bounds for length {length}");
        }
    }
}

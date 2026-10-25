using Language.Runtime.Exceptions;

namespace Language.Runtime.Values;

public abstract class RuntimeValue
{
    public virtual int Length => throw new LanguageRuntimeException(
        "Length requires an array, string, or S-expression");

    public virtual int AsInteger()
    {
        throw new LanguageRuntimeException("Expected an integer value");
    }

    public virtual RuntimeValue GetElement(int index)
    {
        throw new LanguageRuntimeException("Indexing requires an array, string, or S-expression");
    }

    public virtual void SetElement(int index, RuntimeValue value)
    {
        throw new LanguageRuntimeException("Element assignment requires an array, string, or S-expression");
    }

    protected static void ValidateIndex(int index, int length)
    {
        if (index < 0 || index >= length)
        {
            throw new LanguageRuntimeException($"Index {index} is out of bounds for length {length}");
        }
    }
}

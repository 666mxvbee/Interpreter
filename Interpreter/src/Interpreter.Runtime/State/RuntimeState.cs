using Interpreter.Runtime.Exceptions;

namespace Interpreter.Runtime.State;

internal sealed class RuntimeState
{
    private readonly Dictionary<string, int> _variables =
        new(StringComparer.Ordinal);

    public int GetValue(string variableName)
    {
        if (_variables.TryGetValue(variableName, out int value))
        {
            return value;
        }

        throw new InterpreterRuntimeException(
            $"Variable {variableName} is not defined");
    }

    public void SetValue(string variableName, int value)
    {
        _variables[variableName] = value;
    }
}
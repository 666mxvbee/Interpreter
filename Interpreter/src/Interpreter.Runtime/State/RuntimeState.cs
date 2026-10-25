using Interpreter.Runtime.Exceptions;
using Language.Runtime.Values;

namespace Interpreter.Runtime.State;

internal sealed class RuntimeState
{
    private readonly Dictionary<string, RuntimeValue> _variables =
        new(StringComparer.Ordinal);

    public RuntimeValue GetValue(string variableName)
    {
        if (_variables.TryGetValue(variableName, out RuntimeValue? value))
        {
            return value;
        }

        throw new InterpreterRuntimeException(
            $"Variable {variableName} is not defined");
    }

    public void SetValue(string variableName, RuntimeValue value)
    {
        _variables[variableName] = value;
    }
}

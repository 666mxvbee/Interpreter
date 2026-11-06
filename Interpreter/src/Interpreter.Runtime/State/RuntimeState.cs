using Interpreter.Runtime.Exceptions;
using Language.Runtime.Values;

namespace Interpreter.Runtime.State;

internal sealed class RuntimeState
{
    private readonly List<Dictionary<string, RuntimeValue>> _scopes =
        new() { new(StringComparer.Ordinal) };

    public int ScopeDepth => _scopes.Count;

    public RuntimeValue GetValue(string variableName)
    {
        for (int index = _scopes.Count - 1; index >= 0; index--)
        {
            if (_scopes[index].TryGetValue(variableName, out RuntimeValue? value))
            {
                return value;
            }
        }

        throw new InterpreterRuntimeException(
            $"Variable {variableName} is not defined");
    }

    public void SetValue(string variableName, RuntimeValue value)
    {
        for (int index = _scopes.Count - 1; index >= 0; index--)
        {
            if (_scopes[index].ContainsKey(variableName))
            {
                _scopes[index][variableName] = value;
                return;
            }
        }

        _scopes[0][variableName] = value;
    }

    public void PushScope(IReadOnlyDictionary<string, RuntimeValue> bindings)
    {
        _scopes.Add(new Dictionary<string, RuntimeValue>(bindings, StringComparer.Ordinal));
    }

    public void PopScope()
    {
        if (_scopes.Count == 1)
        {
            throw new InvalidOperationException("Cannot remove the global scope");
        }

        _scopes.RemoveAt(_scopes.Count - 1);
    }

    public void RestoreScopeDepth(int depth)
    {
        while (_scopes.Count > depth)
        {
            PopScope();
        }
    }
}

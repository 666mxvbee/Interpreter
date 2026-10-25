using Language.Runtime.Exceptions;
using Language.Runtime.Formatting;
using Language.Runtime.Values;

namespace StackMachine.Core.Execution;

internal sealed class MachineState
{
    private readonly Stack<RuntimeValue> _stack = new();
    private readonly Dictionary<string, RuntimeValue> _memory = new(StringComparer.Ordinal);
    private readonly Queue<RuntimeValue> _input;
    private readonly List<(RuntimeValue Value, int Instruction)> _output = new();

    public MachineState(IEnumerable<RuntimeValue> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = new Queue<RuntimeValue>(input);
    }

    public int ProgramCounter { get; private set; }

    public int StackCount => _stack.Count;

    public void Push(RuntimeValue value)
    {
        _stack.Push(value);
    }

    public bool TryPop(out RuntimeValue? value)
    {
        return _stack.TryPop(out value);
    }

    public bool TryRead(out RuntimeValue? value)
    {
        return _input.TryDequeue(out value);
    }

    public bool TryLoad(string variableName, out RuntimeValue? value)
    {
        return _memory.TryGetValue(variableName, out value);
    }

    public void Store(string variableName, RuntimeValue value)
    {
        _memory[variableName] = value;
    }

    public void Write(RuntimeValue value)
    {
        _output.Add((value, ProgramCounter));
    }

    public void MoveNext()
    {
        ProgramCounter++;
    }

    public void JumpTo(int target)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(target);
        ProgramCounter = target;
    }

    public IReadOnlyList<RuntimeValue> CreateOutputSnapshot()
    {
        foreach ((RuntimeValue value, int instruction) in _output)
        {
            try
            {
                RuntimeValueFormatter.Format(value);
            }
            catch (LanguageRuntimeException exception)
            {
                throw new MachineExecutionException($"{exception.Message} at instruction {instruction}", exception);
            }
        }

        return _output.Select(item => item.Value).ToArray();
    }
}

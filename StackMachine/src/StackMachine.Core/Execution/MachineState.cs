namespace StackMachine.Core.Execution;

internal sealed class MachineState
{
    private readonly Stack<int> _stack = new();
    private readonly Dictionary<string, int> _memory = new(StringComparer.Ordinal);
    private readonly Queue<int> _input;
    private readonly List<int> _output = new();

    public MachineState(IEnumerable<int> input)
    {
        ArgumentNullException.ThrowIfNull(input);

        _input = new Queue<int>(input);
    }

    public int ProgramCounter { get; private set; }

    public void Push(int value)
    {
        _stack.Push(value);
    }

    public bool TryPop(out int value)
    {
        return _stack.TryPop(out value);
    }

    public bool TryRead(out int value)
    {
        return _input.TryDequeue(out value);
    }

    public bool TryLoad(string variableName, out int value)
    {
        ArgumentNullException.ThrowIfNull(variableName);

        return _memory.TryGetValue(variableName, out value);
    }

    public void Store(string variableName, int value)
    {
        ArgumentNullException.ThrowIfNull(variableName);

        _memory[variableName] = value;
    }

    public void Write(int value)
    {
        _output.Add(value);
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

    public IReadOnlyList<int> CreateOutputSnapshot()
    {
        return _output.ToArray();
    }
}
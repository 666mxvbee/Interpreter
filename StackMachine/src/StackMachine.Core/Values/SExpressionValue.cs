using StackMachine.Core.Execution;

namespace StackMachine.Core.Values;

public sealed class SExpressionValue : RuntimeValue
{
    private readonly RuntimeValue[] _arguments;

    public SExpressionValue(string name, RuntimeValue[] arguments)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(arguments);

        Name = name;
        _arguments = arguments;
    }

    public string Name { get; }

    public override int Length => _arguments.Length;

    public override RuntimeValue GetElement(int index)
    {
        ValidateIndex(index, Length);

        return _arguments[index];
    }

    public override void SetElement(int index, RuntimeValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        ValidateIndex(index, Length);

        _arguments[index] = value;
    }

    public override int CompareValue(RuntimeValue other)
    {
        throw new MachineExecutionException("S-expressions cannot be compared");
    }

    public override string ToString()
    {
        return Format(new HashSet<RuntimeValue>());
    }

    internal override string Format(HashSet<RuntimeValue> active)
    {
        if (!active.Add(this))
        {
            throw new MachineExecutionException("Cannot write a cyclic value");
        }

        try
        {
            if (Name == "nil" && Length == 0)
            {
                return "{}";
            }

            if (Name == "cons" && Length == 2)
            {
                return FormatList(active);
            }

            if (Length == 0)
            {
                return Name;
            }

            var arguments = new string[Length];

            for (int index = 0; index < Length; index++)
            {
                arguments[index] = _arguments[index].Format(active);
            }

            return $"{Name} ({string.Join(", ", arguments)})";
        }
        finally
        {
            active.Remove(this);
        }
    }

    private string FormatList(HashSet<RuntimeValue> active)
    {
        var elements = new List<string>();
        var tails = new List<RuntimeValue>();
        SExpressionValue cell = this;

        try
        {
            while (true)
            {
                elements.Add(cell._arguments[0].Format(active));

                if (cell._arguments[1] is not SExpressionValue tail)
                {
                    throw new MachineExecutionException("List tail must be a list");
                }

                if (tail.Name == "nil" && tail.Length == 0)
                {
                    return $"{{{string.Join(", ", elements)}}}";
                }

                if (tail.Name != "cons" || tail.Length != 2)
                {
                    throw new MachineExecutionException("List tail must be a list");
                }

                if (!active.Add(tail))
                {
                    throw new MachineExecutionException("Cannot write a cyclic list");
                }

                tails.Add(tail);
                cell = tail;
            }
        }
        finally
        {
            foreach (RuntimeValue tail in tails)
            {
                active.Remove(tail);
            }
        }
    }
}

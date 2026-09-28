using StackMachine.Core.Instructions;

namespace StackMachine.Core.Execution;

internal sealed class LabelTable
{
    private readonly Dictionary<string, int> _labels;

    private LabelTable(Dictionary<string, int> labels)
    {
        _labels = labels;
    }

    public static LabelTable Create(IReadOnlyList<Instruction> program)
    {
        ArgumentNullException.ThrowIfNull(program);

        var labels = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int index = 0; index < program.Count; index++)
        {
            if (program[index] is not LabelInstruction labelInstruction)
            {
                continue;
            }

            if (!labels.TryAdd(labelInstruction.Name, index))
            {
                int previousIndex = labels[labelInstruction.Name];

                throw new MachineExecutionException(
                    $"Duplicate label \"{labelInstruction.Name}\" "
                    + $"at instructions {previousIndex} and {index}");
            }
        }

        return new LabelTable(labels);
    }

    public bool TryResolve(string name, out int instructionIndex)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _labels.TryGetValue(name, out instructionIndex);
    }
}
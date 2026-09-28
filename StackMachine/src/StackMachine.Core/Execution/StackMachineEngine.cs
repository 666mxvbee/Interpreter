using StackMachine.Core.Instructions;

namespace StackMachine.Core.Execution;

public sealed class StackMachineEngine
{
    public IReadOnlyList<int> Execute(
        IReadOnlyList<Instruction> program,
        IEnumerable<int> input)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(input);

        var labelTable = LabelTable.Create(program);
        var state = new MachineState(input);

        while (state.ProgramCounter < program.Count)
        {
            Instruction instruction =
                program[state.ProgramCounter];

            ExecuteInstruction(
                instruction,
                state,
                labelTable);
        }

        return state.CreateOutputSnapshot();
    }

    private static void ExecuteInstruction(
        Instruction instruction,
        MachineState state,
        LabelTable labelTable)
    {
        switch (instruction)
        {
            case ReadInstruction:
                ExecuteRead(state);
                break;

            case WriteInstruction:
                ExecuteWrite(state);
                break;

            case LoadInstruction loadInstruction:
                ExecuteLoad(loadInstruction, state);
                break;

            case StoreInstruction storeInstruction:
                ExecuteStore(storeInstruction, state);
                break;

            case ConstantInstruction constantInstruction:
                state.Push(constantInstruction.Value);
                state.MoveNext();
                break;

            case BinaryOperationInstruction binaryOperation:
                ExecuteBinaryOperation(binaryOperation, state);
                break;

            case LabelInstruction:
                state.MoveNext();
                break;

            case JumpInstruction jumpInstruction:
                ExecuteJump(
                    jumpInstruction.Target,
                    state,
                    labelTable);
                break;

            case JumpIfZeroInstruction jumpInstruction:
                ExecuteConditionalJump(
                    jumpInstruction.Target,
                    jumpWhenZero: true,
                    state,
                    labelTable);
                break;

            case JumpIfNotZeroInstruction jumpInstruction:
                ExecuteConditionalJump(
                    jumpInstruction.Target,
                    jumpWhenZero: false,
                    state,
                    labelTable);
                break;

            default:
                throw CreateError(
                    state,
                    $"Unsupported instruction type \"{instruction.GetType().Name}\"");
        }
    }

    private static void ExecuteRead(MachineState state)
    {
        if (!state.TryRead(out int value))
        {
            throw CreateError(
                state,
                "READ requires an input value");
        }

        state.Push(value);
        state.MoveNext();
    }

    private static void ExecuteWrite(MachineState state)
    {
        int value = Pop(state);

        state.Write(value);
        state.MoveNext();
    }

    private static void ExecuteLoad(
        LoadInstruction instruction,
        MachineState state)
    {
        if (!state.TryLoad(
                instruction.VariableName,
                out int value))
        {
            throw CreateError(
                state,
                $"Undefined variable \"{instruction.VariableName}\"");
        }

        state.Push(value);
        state.MoveNext();
    }

    private static void ExecuteStore(
        StoreInstruction instruction,
        MachineState state)
    {
        int value = Pop(state);

        state.Store(instruction.VariableName, value);
        state.MoveNext();
    }

    private static void ExecuteBinaryOperation(
        BinaryOperationInstruction instruction,
        MachineState state)
    {
        int right = Pop(state);
        int left = Pop(state);

        try
        {
            int result = BinaryOperatorEvaluator.Evaluate(
                instruction.Operator,
                left,
                right);

            state.Push(result);
            state.MoveNext();
        }
        catch (MachineExecutionException exception)
        {
            throw CreateError(state, exception.Message);
        }
    }

    private static void ExecuteJump(
        string target,
        MachineState state,
        LabelTable labelTable)
    {
        int instructionIndex = Resolve(
            target,
            state,
            labelTable);

        state.JumpTo(instructionIndex);
    }

    private static void ExecuteConditionalJump(
        string target,
        bool jumpWhenZero,
        MachineState state,
        LabelTable labelTable)
    {
        int condition = Pop(state);

        bool shouldJump = jumpWhenZero
            ? condition == 0
            : condition != 0;

        if (shouldJump)
        {
            ExecuteJump(target, state, labelTable);
        }
        else
        {
            state.MoveNext();
        }
    }

    private static int Pop(MachineState state)
    {
        if (!state.TryPop(out int value))
        {
            throw CreateError(
                state,
                "Exhausted stack");
        }

        return value;
    }

    private static int Resolve(
        string target,
        MachineState state,
        LabelTable labelTable)
    {
        if (!labelTable.TryResolve(
                target,
                out int instructionIndex))
        {
            throw CreateError(
                state,
                $"Undefined label \"{target}\"");
        }

        return instructionIndex;
    }

    private static MachineExecutionException CreateError(
        MachineState state,
        string message)
    {
        return new MachineExecutionException(
            $"{message} at instruction "
            + $"{state.ProgramCounter}");
    }
}
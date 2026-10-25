using Language.Runtime.Exceptions;
using Language.Runtime.Values;
using StackMachine.Core.Instructions;

namespace StackMachine.Core.Execution;

public sealed class StackMachineEngine
{
    public IReadOnlyList<RuntimeValue> Execute(IReadOnlyList<Instruction> program, IEnumerable<RuntimeValue> input)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(input);
        var labels = LabelTable.Create(program);
        var state = new MachineState(input);

        while (state.ProgramCounter < program.Count)
        {
            try
            {
                ExecuteInstruction(program[state.ProgramCounter], state, labels);
            }
            catch (LanguageRuntimeException exception)
            {
                throw new MachineExecutionException(
                    $"{exception.Message} at instruction {state.ProgramCounter}", exception);
            }
        }

        return state.CreateOutputSnapshot();
    }

    private static void ExecuteInstruction(Instruction instruction, MachineState state, LabelTable labels)
    {
        switch (instruction)
        {
            case ReadInstruction:
                if (!state.TryRead(out RuntimeValue? input) || input is null)
                {
                    throw new LanguageRuntimeException("READ requires an input value");
                }

                state.Push(input);
                break;
            case WriteInstruction:
                state.Write(Pop(state));
                break;
            case ConstantInstruction constant:
                state.Push(new IntegerValue(constant.Value));
                break;
            case StringInstruction text:
                state.Push(new StringValue(text.Value));
                break;
            case ArrayInstruction array:
                state.Push(new ArrayValue(PopElements(array.Count, state)));
                break;
            case SExpressionInstruction constructor:
                if (string.IsNullOrWhiteSpace(constructor.Tag))
                {
                    throw new LanguageRuntimeException("Constructor tag cannot be empty");
                }

                state.Push(new SExpressionValue(constructor.Tag, PopElements(constructor.ArgumentCount, state)));
                break;
            case LengthInstruction:
                state.Push(new IntegerValue(Pop(state).GetLength()));
                break;
            case ElementInstruction:
                RequireStack(state, 2);
                int index = Pop(state).AsInteger();
                state.Push(Pop(state).GetElement(index));
                break;
            case SetElementInstruction:
                RequireStack(state, 3);
                RuntimeValue value = Pop(state);
                RuntimeValue position = Pop(state);
                RuntimeValue destination = Pop(state);
                destination.SetElement(position.AsInteger(), value);
                break;
            case LoadInstruction load:
                if (!state.TryLoad(load.VariableName, out RuntimeValue? loaded) || loaded is null)
                {
                    throw new LanguageRuntimeException($"Undefined variable \"{load.VariableName}\"");
                }

                state.Push(loaded);
                break;
            case StoreInstruction store:
                state.Store(store.VariableName, Pop(state));
                break;
            case BinaryOperationInstruction binary:
                RequireStack(state, 2);
                RuntimeValue right = Pop(state);
                RuntimeValue left = Pop(state);
                state.Push(BinaryOperatorEvaluator.Evaluate(binary.Operator, left, right));
                break;
            case LabelInstruction:
                break;
            case JumpInstruction jump:
                state.JumpTo(Resolve(jump.Target, labels));
                return;
            case JumpIfZeroInstruction jump:
                ExecuteConditionalJump(jump.Target, true, state, labels);
                return;
            case JumpIfNotZeroInstruction jump:
                ExecuteConditionalJump(jump.Target, false, state, labels);
                return;
            case null:
                throw new LanguageRuntimeException("Instruction cannot be null");
            default:
                throw new LanguageRuntimeException($"Unsupported instruction type {instruction.GetType().Name}");
        }

        state.MoveNext();
    }

    private static void ExecuteConditionalJump(string target, bool jumpWhenZero, MachineState state, LabelTable labels)
    {
        bool isZero = Pop(state).AsInteger() == 0;

        if (isZero == jumpWhenZero)
        {
            state.JumpTo(Resolve(target, labels));
        }
        else
        {
            state.MoveNext();
        }
    }

    private static RuntimeValue Pop(MachineState state)
    {
        if (!state.TryPop(out RuntimeValue? value) || value is null)
        {
            throw new LanguageRuntimeException("Exhausted stack");
        }

        return value;
    }

    private static RuntimeValue[] PopElements(int count, MachineState state)
    {
        if (count < 0)
        {
            throw new LanguageRuntimeException("Element count cannot be negative");
        }

        RequireStack(state, count);
        var values = new RuntimeValue[count];

        for (int index = count - 1; index >= 0; index--)
        {
            values[index] = Pop(state);
        }

        return values;
    }

    private static void RequireStack(MachineState state, int count)
    {
        if (state.StackCount < count)
        {
            throw new LanguageRuntimeException("Exhausted stack");
        }
    }

    private static int Resolve(string target, LabelTable labels)
    {
        if (!labels.TryResolve(target, out int index))
        {
            throw new LanguageRuntimeException($"Undefined label \"{target}\"");
        }

        return index;
    }
}

using StackMachine.Core.Instructions;
using System.Text.Json;

namespace StackMachine.Core.Serialization;

public static class MachineProgramJsonParser
{
    public static IReadOnlyList<Instruction> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new MachineProgramJsonException(
                $"$: expected array, got {root.ValueKind}");
        }

        var instructions = new List<Instruction>(
            root.GetArrayLength());

        int index = 0;

        foreach (JsonElement element in root.EnumerateArray())
        {
            instructions.Add(
                ParseInstruction(element, $"[{index}]"));

            index++;
        }

        return instructions.ToArray();
    }

    private static Instruction ParseInstruction(
        JsonElement element,
        string path)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            string instructionName = element.GetString()
                                     ?? throw new MachineProgramJsonException(
                                         $"{path}: instruction cannot be null");

            return instructionName switch
            {
                "READ" => new ReadInstruction(),
                "WRITE" => new WriteInstruction(),

                _ => throw new MachineProgramJsonException(
                    $"{path}: unknown instruction: \"{instructionName}\""),
            };
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new MachineProgramJsonException(
                $"{path}: expected a string or an object, got {element.ValueKind}");
        }

        JsonProperty property = GetOnlyProperty(element, path);
        string propertyPath = $"{path}.{property.Name}";

        return property.Name switch
        {
            "LD" => new LoadInstruction(
                ParseStringArgument(property.Value, propertyPath)),

            "ST" => new StoreInstruction(
                ParseStringArgument(property.Value, propertyPath)),

            "CONST" => new ConstantInstruction(
                ParseConstant(property.Value, propertyPath)),

            "BINOP" => new BinaryOperationInstruction(
                ParseBinaryOperator(property.Value, propertyPath)),

            "LABEL" => new LabelInstruction(
                ParseStringArgument(property.Value, propertyPath)),

            "JMP" => new JumpInstruction(
                ParseStringArgument(property.Value, propertyPath)),

            "JZ" => new JumpIfZeroInstruction(
                ParseStringArgument(property.Value, propertyPath)),

            "JNZ" => new JumpIfNotZeroInstruction(
                ParseStringArgument(property.Value, propertyPath)),

            _ => throw new MachineProgramJsonException(
                $"{path}: unknown instruction \"{property.Name}\""),
        };
    }

    private static int ParseConstant(
        JsonElement element,
        string path)
    {
        if (element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out int value))
        {
            return value;
        }

        throw new MachineProgramJsonException(
            $"{path}: constant must be a 32-bit integer");
    }

    private static BinaryOperator ParseBinaryOperator(
        JsonElement element,
        string path)
    {
        string value = ParseStringArgument(element, path);

        return value switch
        {
            "!!" => BinaryOperator.LogicalOr,
            "&&" => BinaryOperator.LogicalAnd,

            "==" => BinaryOperator.Equal,
            "!=" => BinaryOperator.NotEqual,
            ">" => BinaryOperator.GreaterThan,
            ">=" => BinaryOperator.GreaterThanOrEqual,
            "<" => BinaryOperator.LessThan,
            "<=" => BinaryOperator.LessThanOrEqual,

            "+" => BinaryOperator.Add,
            "-" => BinaryOperator.Subtract,
            "*" => BinaryOperator.Multiply,
            "/" => BinaryOperator.Divide,
            "%" => BinaryOperator.Remainder,

            _ => throw new MachineProgramJsonException(
                $"{path}: unknown binary operator \"{value}\""),
        };
    }

    private static string ParseStringArgument(
        JsonElement element,
        string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new MachineProgramJsonException(
                $"{path}: expected a string, got {element.ValueKind}");
        }

        return element.GetString()
            ?? throw new MachineProgramJsonException(
                $"{path}: string argument cannot be null");
    }

    private static JsonProperty GetOnlyProperty(
        JsonElement element,
        string path)
    {
        EnsureObjectPropertyCount(element, path, 1);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            return property;
        }

        throw new MachineProgramJsonException(
            $"{path}: instruction object cannot be empty");
    }

    private static void EnsureObjectPropertyCount(
        JsonElement element,
        string path,
        int expectedCount)
    {
        EnsureObject(element, path);

        int actualCount = element.GetPropertyCount();

        if (actualCount != expectedCount)
        {
            throw new MachineProgramJsonException(
                $"{path}: expected {expectedCount}, properties "
                + $"got {actualCount}");
        }
    }

    private static void EnsureObject(
        JsonElement element,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new MachineProgramJsonException(
                $"{path}: expected an object, got {element.ValueKind}");
        }
    }
}
using Interpreter.Ast.Expressions;
using Interpreter.Ast.Statements;
using System.Text.Json;

namespace Interpreter.Ast.Serialization;

public static class AstJsonParser
{
    private const int MaximumDepth = 4096;

    public static Statement Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var options = new JsonDocumentOptions
        {
            MaxDepth = MaximumDepth,
        };

        using var document = JsonDocument.Parse(json, options);

        return ParseStatement(document.RootElement, "$");
    }

    private static Statement ParseStatement(
        JsonElement element,
        string path)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() == "skip"
                ? new SkipStatement()
                : throw new AstJsonException(
                    $"{path}: expected \"skip\", got {element.GetRawText()}");
        }

        JsonProperty property = GetOnlyProperty(element, path);
        string propertyPath = $"{path}.{property.Name}";

        return property.Name switch
        {
            "seq" => ParseSequence(property.Value, propertyPath),
            "assn" => ParseAssignment(property.Value, propertyPath),
            "read" => new ReadStatement(
                ParseIdentifier(property.Value, propertyPath)),
            "write" => new WriteStatement(
                ParseExpression(property.Value, propertyPath)),
            "while" => ParseWhile(property.Value, propertyPath),
            "do" => ParseDoWhile(property.Value, propertyPath),
            "if" => ParseIf(property.Value, propertyPath),

            _ => throw new AstJsonException(
                $"{path}: unknown statement {property.Name}"),
        };
    }

    private static SequenceStatement ParseSequence(
        JsonElement element,
        string path)
    {
        EnsureObjectPropertyCount(element, path, 2);

        return new SequenceStatement(
            ParseStatement(
                GetRequiredProperty(element, "left", path),
                $"{path}.left"),
            ParseStatement(
                GetRequiredProperty(element, "right", path),
                $"{path}.right"));
    }

    private static AssignmentStatement ParseAssignment(
        JsonElement element,
        string path)
    {
        EnsureObjectPropertyCount(element, path, 2);

        return new AssignmentStatement(
            ParseIdentifier(
                GetRequiredProperty(element, "dst", path),
                $"{path}.dst"),
            ParseExpression(
                GetRequiredProperty(element, "src", path),
                $"{path}.src"));
    }

    private static WhileStatement ParseWhile(
        JsonElement element,
        string path)
    {
        EnsureObjectPropertyCount(element, path, 2);

        return new WhileStatement(
            ParseExpression(
                GetRequiredProperty(element, "cond", path),
                $"{path}.cond"),
            ParseStatement(
                GetRequiredProperty(element, "body", path),
                $"{path}.body"));
    }

    private static DoWhileStatement ParseDoWhile(
        JsonElement element,
        string path)
    {
        EnsureObjectPropertyCount(element, path, 2);

        return new DoWhileStatement(
            ParseStatement(
                GetRequiredProperty(element, "body", path),
                $"{path}.body"),
            ParseExpression(
                GetRequiredProperty(element, "cond", path),
                $"{path}.cond"));
    }

    private static IfStatement ParseIf(
        JsonElement element,
        string path)
    {
        EnsureObjectPropertyCount(element, path, 3);

        return new IfStatement(
            ParseExpression(
                GetRequiredProperty(element, "cond", path),
                $"{path}.cond"),
            ParseStatement(
                GetRequiredProperty(element, "then", path),
                $"{path}.then"),
            ParseStatement(
                GetRequiredProperty(element, "else", path),
                $"{path}.else"));
    }

    private static Expression ParseExpression(
        JsonElement element,
        string path)
    {
        EnsureObject(element, path);

        if (element.TryGetProperty("const", out JsonElement constant))
        {
            EnsureObjectPropertyCount(element, path, 1);

            return new ConstantExpression(
                ParseConstant(constant, $"{path}.const"));
        }

        if (element.TryGetProperty("var", out JsonElement variable))
        {
            EnsureObjectPropertyCount(element, path, 1);

            return new VariableExpression(
                ParseIdentifier(variable, $"{path}.var"));
        }

        if (element.TryGetProperty("binop", out JsonElement binaryOperator))
        {
            EnsureObjectPropertyCount(element, path, 3);

            return new BinaryExpression(
                ParseBinaryOperator(binaryOperator, $"{path}.binop"),
                ParseExpression(
                    GetRequiredProperty(element, "left", path),
                    $"{path}.left"),
                ParseExpression(
                    GetRequiredProperty(element, "right", path),
                    $"{path}.right"));
        }

        throw new AstJsonException($"{path}: unknown expression");
    }

    private static BinaryOperator ParseBinaryOperator(
        JsonElement element,
        string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new AstJsonException(
                $"{path}: binary operator must be a string");
        }

        return element.GetString() switch
        {
            "!!" => BinaryOperator.LogicalOr,
            "&&" => BinaryOperator.LogicalAnd,
            "==" => BinaryOperator.Equal,
            "!=" => BinaryOperator.NotEqual,
            "<" => BinaryOperator.LessThan,
            "<=" => BinaryOperator.LessThanOrEqual,
            ">" => BinaryOperator.GreaterThan,
            ">=" => BinaryOperator.GreaterThanOrEqual,
            "+" => BinaryOperator.Add,
            "-" => BinaryOperator.Subtract,
            "*" => BinaryOperator.Multiply,
            "/" => BinaryOperator.Divide,
            "%" => BinaryOperator.Remainder,

            var value => throw new AstJsonException(
                $"{path}: unknown binary operator {value}"),
        };
    }

    private static int ParseConstant(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out int value))
        {
            return value;
        }

        throw new AstJsonException(
            $"{path}: constant must be a 32-bit integer");
    }

    private static string ParseIdentifier(
        JsonElement element,
        string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new AstJsonException(
                $"{path}: identifier must be a string");
        }

        string identifier = element.GetString()
            ?? throw new AstJsonException(
                $"{path}: identifier cannot be null");

        if (!IsValidIdentifier(identifier))
        {
            throw new AstJsonException(
                $"{path}: invalid identifier {identifier}");
        }

        return identifier;
    }

    private static bool IsValidIdentifier(string identifier)
    {
        if (identifier.Length == 0
            || !char.IsAsciiLetterLower(identifier[0]))
        {
            return false;
        }

        for (int index = 1; index < identifier.Length; index++)
        {
            char character = identifier[index];

            if (!char.IsAsciiLetterOrDigit(character)
                && character is not '_' and not '\'')
            {
                return false;
            }
        }

        return true;
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

        throw new AstJsonException(
            $"{path}: statement object cannot be empty");
    }

    private static JsonElement GetRequiredProperty(
        JsonElement element,
        string propertyName,
        string path)
    {
        EnsureObject(element, path);

        if (element.TryGetProperty(propertyName, out JsonElement property))
        {
            return property;
        }

        throw new AstJsonException(
            $"{path}: required property '{propertyName}' not found");
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
            throw new AstJsonException(
                $"{path}: expected {expectedCount} properties "
                + $"got {actualCount}");
        }
    }

    private static void EnsureObject(
        JsonElement element,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new AstJsonException(
                $"{path}: expected an object, got {element.ValueKind}");
        }
    }
}
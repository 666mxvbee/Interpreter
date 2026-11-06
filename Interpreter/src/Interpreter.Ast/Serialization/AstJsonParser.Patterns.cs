using Interpreter.Ast.Expressions;
using Interpreter.Ast.Patterns;
using Interpreter.Ast.Statements;
using System.Text.Json;

namespace Interpreter.Ast.Serialization;

public static partial class AstJsonParser
{
    private static SwitchStatement ParseSwitch(JsonElement element, string path)
    {
        EnsureObjectPropertyCount(element, path, 2);
        Expression scrutinee = ParseExpression(
            GetRequiredProperty(element, "scrutinee", path), $"{path}.scrutinee");
        JsonElement branches = GetRequiredProperty(element, "branches", path);

        if (branches.ValueKind != JsonValueKind.Array || branches.GetArrayLength() == 0)
        {
            throw new AstJsonException($"{path}.branches: expected a non-empty array");
        }

        var result = new List<SwitchBranch>(branches.GetArrayLength());

        foreach (JsonElement branch in branches.EnumerateArray())
        {
            string branchPath = $"{path}.branches[{result.Count}]";
            EnsureObjectPropertyCount(branch, branchPath, 2);
            result.Add(new SwitchBranch(
                ParsePattern(GetRequiredProperty(branch, "pattern", branchPath), $"{branchPath}.pattern"),
                ParseStatement(GetRequiredProperty(branch, "branch", branchPath), $"{branchPath}.branch")));
        }

        return new SwitchStatement(scrutinee, result.ToArray());
    }

    private static Pattern ParsePattern(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return new IntegerPattern(ParseConstant(element, path));
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() switch
            {
                "#int" => new TypePattern(PatternValueType.Integer),
                "#string" => new TypePattern(PatternValueType.String),
                "#array" => new TypePattern(PatternValueType.Array),
                "#sexp" => new TypePattern(PatternValueType.SExpression),
                string text => new StringPattern(text),
                _ => throw new AstJsonException($"{path}: expected a pattern string"),
            };
        }

        EnsureObject(element, path);

        if (element.TryGetProperty("sexp", out JsonElement tag))
        {
            EnsureObjectPropertyCount(element, path, 2);
            return new SExpressionPattern(
                ParseTag(tag, $"{path}.sexp"),
                ParsePatterns(GetRequiredProperty(element, "args", path), $"{path}.args"));
        }

        JsonProperty property = GetOnlyProperty(element, path);
        string propertyPath = $"{path}.{property.Name}";

        switch (property.Name)
        {
            case "wildcard":
                if (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() != 0)
                {
                    throw new AstJsonException($"{propertyPath}: expected an empty array");
                }

                return new WildcardPattern();
            case "bind":
                EnsureObjectPropertyCount(property.Value, propertyPath, 2);
                return new BindPattern(
                    ParseIdentifier(GetRequiredProperty(property.Value, "name", propertyPath), $"{propertyPath}.name"),
                    ParsePattern(GetRequiredProperty(property.Value, "pattern", propertyPath), $"{propertyPath}.pattern"));
            case "array":
                return new ArrayPattern(ParsePatterns(property.Value, propertyPath));
            default:
                throw new AstJsonException($"{path}: unknown pattern {property.Name}");
        }
    }

    private static IReadOnlyList<Pattern> ParsePatterns(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new AstJsonException($"{path}: expected an array of patterns");
        }

        var patterns = new List<Pattern>(element.GetArrayLength());

        foreach (JsonElement item in element.EnumerateArray())
        {
            patterns.Add(ParsePattern(item, $"{path}[{patterns.Count}]"));
        }

        return patterns.ToArray();
    }
}

using Interpreter.Ast.Serialization;
using Interpreter.Ast.Statements;
using Interpreter.Runtime;
using Language.Runtime.Exceptions;
using Language.Runtime.Formatting;
using Language.Runtime.Input;
using Language.Runtime.Values;
using StackMachine.Core.Execution;
using StackMachine.Core.Instructions;
using StackMachine.Core.Serialization;
using System.Text.Json;
using Xunit;

namespace Language.Runtime.Tests;

public sealed class ReferenceCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference.json")));

        foreach (JsonElement item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            string name = item.GetProperty("name").GetString() ?? throw new InvalidDataException("Missing fixture name");
            yield return new object[] { name, "ast" };
            yield return new object[] { name, "sm" };
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExecutionMatchesReference(string name, string executor)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference.json")));
        JsonElement fixture = document.RootElement.GetProperty("cases").EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == name);
        JsonElement expected = fixture.GetProperty(executor);
        string input = fixture.GetProperty("input").GetString() ?? string.Empty;
        string json = expected.GetProperty("json").GetString() ?? string.Empty;
        string output = expected.GetProperty("output").GetString() ?? string.Empty;
        bool fails = !string.IsNullOrEmpty(expected.GetProperty("error").GetString());

        if (executor == "ast")
        {
            Statement program = AstJsonParser.Parse(json);
            using var source = new StringReader(input);
            using var destination = new StringWriter();
            var engine = new InterpreterEngine(source, destination);

            if (fails)
            {
                Assert.ThrowsAny<LanguageRuntimeException>(() => engine.Execute(program));
                Assert.Empty(destination.ToString());
            }
            else
            {
                engine.Execute(program);
                Assert.Equal(output + Environment.NewLine, destination.ToString());
            }
        }
        else
        {
            IReadOnlyList<Instruction> program = MachineProgramJsonParser.Parse(json);
            IReadOnlyList<RuntimeValue> values = ValueInputReader.Parse(input);
            var engine = new StackMachineEngine();

            if (fails)
            {
                Assert.ThrowsAny<LanguageRuntimeException>(() => engine.Execute(program, values));
            }
            else
            {
                Assert.Equal(output, RuntimeValueFormatter.FormatOutput(engine.Execute(program, values)));
            }
        }
    }
}

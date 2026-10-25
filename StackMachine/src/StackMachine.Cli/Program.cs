using Language.Runtime.Exceptions;
using Language.Runtime.Formatting;
using Language.Runtime.Values;
using StackMachine.Core.Execution;
using StackMachine.Core.Input;
using StackMachine.Core.Instructions;
using StackMachine.Core.Serialization;
using System.Text.Json;

namespace StackMachine.Cli;

public static class Program
{
    private const int SuccessExitCode = 0;
    private const int ExecutionFailureExitCode = 1;
    private const int InvalidArgumentsExitCode = 2;

    public static int Main(string[] args)
    {
        if (args.Length is < 1 or > 2 || args[0] is "-h" or "--help")
        {
            WriteUsage();
            return InvalidArgumentsExitCode;
        }

        try
        {
            string json = File.ReadAllText(args[0]);
            IReadOnlyList<Instruction> program = MachineProgramJsonParser.Parse(json);
            string inputText = args.Length == 2 ? File.ReadAllText(args[1]) : Console.In.ReadToEnd();
            IReadOnlyList<RuntimeValue> input = MachineInputParser.Parse(inputText);
            var engine = new StackMachineEngine();
            IReadOnlyList<RuntimeValue> output = engine.Execute(program, input);

            if (output.Count > 0)
            {
                string formatted = RuntimeValueFormatter.FormatOutput(output);
                Console.WriteLine(formatted);
            }

            return SuccessExitCode;
        }
        catch (JsonException exception)
        {
            return ReportError("Invalid JSON", exception);
        }
        catch (MachineProgramJsonException exception)
        {
            return ReportError("Invalid program", exception);
        }
        catch (MachineInputException exception)
        {
            return ReportError("Invalid input", exception);
        }
        catch (LanguageRuntimeException exception)
        {
            return ReportError("Execution error", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ReportError("I/O error", exception);
        }
        catch (IOException exception)
        {
            return ReportError("I/O error", exception);
        }
        catch (ArgumentException exception)
        {
            return ReportError("Invalid argument", exception);
        }
    }

    private static int ReportError(string category, Exception exception)
    {
        Console.Error.WriteLine($"{category}: {exception.Message}");
        return ExecutionFailureExitCode;
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine("Usage: StackMachine.Cli <program.json> [input.txt]");
        Console.Error.WriteLine("  input.txt: comma-separated values, e.g. 42, \"abc\", [1, 2]");
        Console.Error.WriteLine("  if input.txt is omitted, input is read from stdin");
    }
}

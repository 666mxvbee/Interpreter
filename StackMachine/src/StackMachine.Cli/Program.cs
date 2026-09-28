using StackMachine.Core.Execution;
using StackMachine.Core.Input;
using StackMachine.Core.Instructions;
using StackMachine.Core.Serialization;
using System.Globalization;
using System.Text.Json;

namespace StackMachine.Cli;

public static class Program
{
    private const int SuccessExitCode = 0;
    private const int ExecutionFailureExitCode = 1;
    private const int InvalidArgumentsExitCode = 2;

    public static int Main(string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            WriteUsage();

            return InvalidArgumentsExitCode;
        }

        if (args[0] is "-h" or "--help")
        {
            WriteUsage();

            return InvalidArgumentsExitCode;
        }

        string programPath = args[0];
        string? inputPath = args.Length == 2
            ? args[1]
            : null;

        return Run(programPath, inputPath);
    }

    private static int Run(
        string programPath,
        string? inputPath)
    {
        try
        {
            string json = File.ReadAllText(programPath);

            string inputText = inputPath is null
                ? Console.In.ReadToEnd()
                : File.ReadAllText(inputPath);

            IReadOnlyList<Instruction> program =
                MachineProgramJsonParser.Parse(json);

            IReadOnlyList<int> input =
                MachineInputParser.Parse(inputText);

            var engine = new StackMachineEngine();

            IReadOnlyList<int> output =
                engine.Execute(program, input);

            WriteOutput(output);

            return SuccessExitCode;
        }
        catch (JsonException exception)
        {
            return ReportError(
                "Invalid JSON",
                exception);
        }
        catch (MachineProgramJsonException exception)
        {
            return ReportError(
                "Invalid program",
                exception);
        }
        catch (MachineInputException exception)
        {
            return ReportError(
                "Invalid input",
                exception);
        }
        catch (MachineExecutionException exception)
        {
            return ReportError(
                "Execution error",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ReportError(
                "I/O error",
                exception);
        }
        catch (IOException exception)
        {
            return ReportError(
                "I/O error",
                exception);
        }
    }

    private static void WriteOutput(
        IReadOnlyList<int> output)
    {
        IEnumerable<string> formattedValues =
            output.Select(value =>
                value.ToString(
                    CultureInfo.InvariantCulture));

        Console.WriteLine(
            string.Join("; ", formattedValues));
    }

    private static int ReportError(
        string category,
        Exception e)
    {
        Console.Error.WriteLine(
            $"{category}: {e.Message}");

        return ExecutionFailureExitCode;
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine("Usage: StackMachine.Cli <program.json> [input]");

        Console.Error.WriteLine("input: comma-separated integers,for example \"-52, 67\"");

        Console.Error.WriteLine("if input.txt is omitted, input is read from stdin");
    }
}
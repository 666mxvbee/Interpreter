using Interpreter.Ast.Serialization;
using Interpreter.Ast.Statements;
using Interpreter.Runtime;
using System.Text.Json;

namespace Interpreter.Cli;

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

        string programPath = args[0];
        string? inputPath = args.Length == 2
            ? args[1]
            : null;

        return Run(programPath, inputPath);
    }

    private static int Run(string programPath, string? inputPath)
    {
        try
        {
            string json = File.ReadAllText(programPath);
            Statement program = AstJsonParser.Parse(json);

            if (inputPath is null)
            {
                Execute(program, Console.In);
            }
            else
            {
                using StreamReader input = File.OpenText(inputPath);

                Execute(program, input);
            }

            return SuccessExitCode;
        }
        catch (JsonException exception)
        {
            return ReportError("Invalid JSON", exception);
        }
        catch (AstJsonException exception)
        {
            return ReportError("Invalid AST", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ReportError("I/O error", exception);
        }
        catch (IOException exception)
        {
            return ReportError("I/O error", exception);
        }
    }

    private static void Execute(Statement program, TextReader input)
    {
        var engine = new InterpreterEngine(input, Console.Out);

        engine.Execute(program);
    }

    private static int ReportError(string category, Exception exception)
    {
        Console.Error.WriteLine($"{category}: {exception.Message}");

        return ExecutionFailureExitCode;
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine(
            "Usage: Interpreter.Cli <program.json> [input.txt]");
    }
}
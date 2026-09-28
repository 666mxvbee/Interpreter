using Interpreter.Runtime.Exceptions;
using System.Globalization;

namespace Interpreter.Runtime.Input;

internal sealed class IntegerInputReader
{
    private readonly TextReader _reader;
    private readonly Queue<string> _tokens = new();

    public IntegerInputReader(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _reader = reader;
    }

    public int Read()
    {
        while (_tokens.Count == 0)
        {
            string? line = _reader.ReadLine();

            if (line is null)
            {
                throw new InterpreterRuntimeException(
                    "Input ended before an integer could be read");
            }

            foreach (string token in line.Split(
                         (char[]?)null,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                _tokens.Enqueue(token);
            }
        }

        string value = _tokens.Dequeue();

        if (int.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int result))
        {
            return result;
        }

        throw new InterpreterRuntimeException(
            $"Expected an integer, got {value}");
    }
}
using Interpreter.Runtime.Exceptions;
using Interpreter.Runtime.Values;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Interpreter.Runtime.Input;

internal sealed class ValueInputReader
{
    private readonly TextReader _reader;
    private bool _hasReadValue;

    public ValueInputReader(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _reader = reader;
    }

    public RuntimeValue Read()
    {
        SkipWhitespace();

        if (_hasReadValue && _reader.Peek() == ',')
        {
            _reader.Read();
        }

        RuntimeValue value = ReadValue();
        int next = _reader.Peek();

        if (next >= 0 && next != ',' && !char.IsWhiteSpace((char)next))
        {
            throw new InterpreterRuntimeException("Expected a separator after input value");
        }

        _hasReadValue = true;

        return value;
    }

    private RuntimeValue ReadValue()
    {
        SkipWhitespace();

        return _reader.Peek() switch
        {
            -1 => throw new InterpreterRuntimeException("Input ended before a value could be read"),
            '"' => ReadString(),
            '[' => ReadArray(),
            _ => ReadInteger(),
        };
    }

    private StringValue ReadString()
    {
        var literal = new StringBuilder();
        literal.Append((char)_reader.Read());
        bool escaped = false;

        while (true)
        {
            int character = _reader.Read();

            if (character < 0)
            {
                throw new InterpreterRuntimeException("Unterminated string in input");
            }

            literal.Append((char)character);

            if (!escaped && character == '"')
            {
                break;
            }

            escaped = !escaped && character == '\\';
        }

        try
        {
            string value = JsonSerializer.Deserialize<string>(literal.ToString())
                ?? throw new InterpreterRuntimeException("Expected a string in input");

            return new StringValue(value);
        }
        catch (JsonException)
        {
            throw new InterpreterRuntimeException("Invalid string literal in input");
        }
    }

    private ArrayValue ReadArray()
    {
        _reader.Read();

        var elements = new List<RuntimeValue> { ReadValue() };

        while (true)
        {
            SkipWhitespace();
            int separator = _reader.Read();

            if (separator == ']')
            {
                return new ArrayValue(elements.ToArray());
            }

            if (separator != ',')
            {
                throw new InterpreterRuntimeException("Expected ',' or ']' in input array");
            }

            elements.Add(ReadValue());
        }
    }

    private IntegerValue ReadInteger()
    {
        var token = new StringBuilder();

        while (_reader.Peek() is int character && character >= 0
               && !char.IsWhiteSpace((char)character) && character is not ',' and not ']')
        {
            token.Append((char)_reader.Read());
        }

        if (int.TryParse(
                token.ToString(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int value))
        {
            return new IntegerValue(value);
        }

        throw new InterpreterRuntimeException("Expected a 32-bit integer, quoted string, or non-empty array in input");
    }

    private void SkipWhitespace()
    {
        while (_reader.Peek() is int character && character >= 0
               && char.IsWhiteSpace((char)character))
        {
            _reader.Read();
        }
    }
}

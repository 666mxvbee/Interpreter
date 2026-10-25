using StackMachine.Core.Values;
using System.Globalization;
using System.Text;

namespace StackMachine.Core.Input;

internal sealed class ValueInputReader
{
    private readonly TextReader _reader;
    private readonly List<int> _buffer = new();
    private int _position;
    private bool _hasReadValue;

    public ValueInputReader(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _reader = reader;
    }

    public bool HasValue
    {
        get
        {
            SkipWhitespace();

            return Peek() >= 0;
        }
    }

    public RuntimeValue Read()
    {
        SkipWhitespace();

        if (_hasReadValue && Peek() == ',')
        {
            ReadCharacter();
        }

        RuntimeValue value = ReadValue();
        int next = Peek();

        if (next >= 0 && next != ',' && !char.IsWhiteSpace((char)next)
            && !(next == '-' && Peek(1) == '-')
            && !(next == '(' && Peek(1) == '*'))
        {
            throw new MachineInputException("Expected a separator after input value");
        }

        _hasReadValue = true;

        return value;
    }

    private RuntimeValue ReadValue()
    {
        SkipWhitespace();

        return Peek() switch
        {
            -1 => throw new MachineInputException("Input ended before a value could be read"),
            '"' => new StringValue(ReadString()),
            '\'' => ReadCharacterValue(),
            '[' => new ArrayValue(ReadElements(']')),
            '{' => ReadList(),
            >= 'A' and <= 'Z' => ReadConstructor(),
            _ => ReadInteger(),
        };
    }

    private string ReadString()
    {
        ReadCharacter();
        var value = new StringBuilder();

        while (true)
        {
            int character = ReadCharacter();

            if (character < 0)
            {
                throw new MachineInputException("Unterminated string in input");
            }

            if (character == '"')
            {
                if (Peek() != '"')
                {
                    return value.ToString();
                }

                ReadCharacter();
            }

            value.Append((char)character);
        }
    }

    private IntegerValue ReadCharacterValue()
    {
        ReadCharacter();
        int character = ReadCharacter();

        if (character == '\'' && Peek() == '\'')
        {
            ReadCharacter();
        }
        else if (character == '\\' && Peek() is 'n' or 't')
        {
            character = ReadCharacter() == 'n' ? '\n' : '\t';
        }

        if (character < 0 || character > 127 || ReadCharacter() != '\'')
        {
            throw new MachineInputException("Expected a single-byte character literal in input");
        }

        return new IntegerValue(character);
    }

    private RuntimeValue[] ReadElements(char closing, bool allowEmpty = true)
    {
        ReadCharacter();
        SkipWhitespace();
        var elements = new List<RuntimeValue>();

        if (Peek() == closing)
        {
            if (!allowEmpty)
            {
                throw new MachineInputException("Expected at least one constructor argument");
            }

            ReadCharacter();

            return elements.ToArray();
        }

        while (true)
        {
            elements.Add(ReadValue());
            SkipWhitespace();
            int separator = ReadCharacter();

            if (separator == closing)
            {
                return elements.ToArray();
            }

            if (separator != ',')
            {
                throw new MachineInputException($"Expected ',' or '{closing}' in input");
            }
        }
    }

    private SExpressionValue ReadList()
    {
        RuntimeValue[] elements = ReadElements('}');
        var result = new SExpressionValue("nil", Array.Empty<RuntimeValue>());

        for (int index = elements.Length - 1; index >= 0; index--)
        {
            result = new SExpressionValue("cons", new RuntimeValue[] { elements[index], result });
        }

        return result;
    }

    private SExpressionValue ReadConstructor()
    {
        var name = new StringBuilder();

        while (Peek() is int character
               && (character is >= 'a' and <= 'z' or >= 'A' and <= 'Z'
                   or >= '0' and <= '9' or '_' or '\''))
        {
            name.Append((char)ReadCharacter());
        }

        int offset = TriviaLength();
        RuntimeValue[] arguments = Array.Empty<RuntimeValue>();

        if (Peek(offset) == '(')
        {
            SkipWhitespace();
            arguments = ReadElements(')', allowEmpty: false);
        }

        return new SExpressionValue(name.ToString(), arguments);
    }

    private IntegerValue ReadInteger()
    {
        var token = new StringBuilder();

        if (Peek() is '-' or '+')
        {
            token.Append((char)ReadCharacter());
        }

        while (Peek() is >= '0' and <= '9')
        {
            token.Append((char)ReadCharacter());
        }

        if (int.TryParse(
                token.ToString(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int value))
        {
            return new IntegerValue(value);
        }

        throw new MachineInputException("Expected an integer, string, character, array, list, or constructor in input");
    }

    private int Peek(int offset = 0)
    {
        while (_buffer.Count <= _position + offset)
        {
            _buffer.Add(_reader.Read());
        }

        return _buffer[_position + offset];
    }

    private int ReadCharacter()
    {
        if (_position == _buffer.Count)
        {
            return _reader.Read();
        }

        int character = _buffer[_position++];

        if (_position == _buffer.Count)
        {
            _buffer.Clear();
            _position = 0;
        }

        return character;
    }

    private void SkipWhitespace()
    {
        int count = TriviaLength();

        for (int index = 0; index < count; index++)
        {
            ReadCharacter();
        }
    }

    private int TriviaLength()
    {
        int offset = 0;

        while (true)
        {
            int character = Peek(offset);

            if (character >= 0 && char.IsWhiteSpace((char)character))
            {
                offset++;
            }
            else if (character == '-' && Peek(offset + 1) == '-')
            {
                while (Peek(offset) >= 0 && Peek(offset) != '\n')
                {
                    offset++;
                }
            }
            else if (character == '(' && Peek(offset + 1) == '*')
            {
                offset += 2;

                while (Peek(offset) != '*' || Peek(offset + 1) != ')')
                {
                    if (Peek(offset) < 0)
                    {
                        throw new MachineInputException("Unterminated comment in input");
                    }

                    offset++;
                }

                offset += 2;
            }
            else
            {
                return offset;
            }
        }
    }
}

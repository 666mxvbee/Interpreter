using Language.Runtime.Exceptions;
using Language.Runtime.Values;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Language.Runtime.Input;

public sealed class ValueInputReader
{
    private readonly TextReader _reader;
    private bool _hasReadValue;
    private bool _lastReadWasWhitespace;
    private int _position;

    public ValueInputReader(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    public static IReadOnlyList<RuntimeValue> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var source = new StringReader(text);
        var reader = new ValueInputReader(source);
        var values = new List<RuntimeValue>();

        while (reader.TryRead(out RuntimeValue? value))
        {
            values.Add(value);
        }

        return values.ToArray();
    }

    public bool TryRead([NotNullWhen(true)] out RuntimeValue? value)
    {
        SkipWhitespace();

        if (_reader.Peek() < 0)
        {
            value = null;
            return false;
        }

        value = Read();
        return true;
    }

    public RuntimeValue Read()
    {
        SkipWhitespace();

        if (_hasReadValue && _reader.Peek() == ',')
        {
            ReadCharacter();
        }

        RuntimeValue value = ReadValue();
        int next = _reader.Peek();

        if (next >= 0 && next != ',' && !char.IsWhiteSpace((char)next) && !_lastReadWasWhitespace)
        {
            throw Error("Expected a separator after input value");
        }

        _hasReadValue = true;
        return value;
    }

    private RuntimeValue ReadValue()
    {
        var pending = new Stack<Container>();
        RuntimeValue? value = null;

        while (true)
        {
            if (value is null)
            {
                SkipWhitespace();
                int next = _reader.Peek();

                if (next < 0)
                {
                    throw Error("Input ended before a value could be read");
                }

                Container? container = null;

                if (next is '[' or '{')
                {
                    ReadCharacter();
                    container = new Container(next == '[' ? ']' : '}', null);
                }
                else if (char.IsAsciiLetterUpper((char)next))
                {
                    string tag = ReadTag();
                    SkipWhitespace();

                    if (_reader.Peek() == '(')
                    {
                        ReadCharacter();
                        container = new Container(')', tag);
                    }
                    else
                    {
                        value = new SExpressionValue(tag, Array.Empty<RuntimeValue>());
                    }
                }
                else
                {
                    value = next == '"' ? ReadString() : ReadInteger();
                }

                if (container is not null)
                {
                    if (pending.Count >= RuntimeLimits.MaximumNestingDepth)
                    {
                        throw Error("Input nesting depth limit exceeded");
                    }

                    SkipWhitespace();

                    if (_reader.Peek() == container.ClosingCharacter)
                    {
                        ReadCharacter();
                        value = container.CreateValue();
                    }
                    else
                    {
                        pending.Push(container);
                        continue;
                    }
                }
            }

            if (value is null)
            {
                throw Error("Expected an input value");
            }

            if (pending.Count == 0)
            {
                return value;
            }

            Container current = pending.Peek();
            current.Elements.Add(value);
            SkipWhitespace();
            int separator = ReadCharacter();

            if (separator == current.ClosingCharacter)
            {
                pending.Pop();
                value = current.CreateValue();
            }
            else if (separator == ',')
            {
                value = null;
            }
            else
            {
                throw Error($"Expected ',' or '{current.ClosingCharacter}' in input value");
            }
        }
    }

    private StringValue ReadString()
    {
        var literal = new StringBuilder();
        literal.Append((char)ReadCharacter());
        bool escaped = false;

        while (true)
        {
            int character = ReadCharacter();

            if (character < 0)
            {
                throw Error("Unterminated string in input");
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
            string text = JsonSerializer.Deserialize<string>(literal.ToString())
                ?? throw Error("Expected a string in input");
            return new StringValue(text);
        }
        catch (JsonException exception)
        {
            throw new LanguageRuntimeException($"Invalid string literal at input position {_position}", exception);
        }
    }

    private IntegerValue ReadInteger()
    {
        var token = new StringBuilder();

        while (_reader.Peek() is int character && character >= 0
               && !char.IsWhiteSpace((char)character) && character is not ',' and not ']' and not '}' and not ')')
        {
            token.Append((char)ReadCharacter());
        }

        if (!int.TryParse(token.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
        {
            throw Error("Expected a 32-bit integer, quoted string, array, list, or constructor in input");
        }

        return new IntegerValue(value);
    }

    private string ReadTag()
    {
        var tag = new StringBuilder();

        while (_reader.Peek() is int character && character >= 0
               && (char.IsAsciiLetterOrDigit((char)character) || character is '_' or '\''))
        {
            tag.Append((char)ReadCharacter());
        }

        return tag.ToString();
    }

    private void SkipWhitespace()
    {
        while (_reader.Peek() is int character && character >= 0 && char.IsWhiteSpace((char)character))
        {
            ReadCharacter();
        }
    }

    private int ReadCharacter()
    {
        int character = _reader.Read();

        if (character >= 0)
        {
            _position++;
            _lastReadWasWhitespace = char.IsWhiteSpace((char)character);
        }

        return character;
    }

    private LanguageRuntimeException Error(string message)
    {
        return new LanguageRuntimeException($"{message} at input position {_position}");
    }

    private sealed class Container
    {
        public Container(char closingCharacter, string? tag)
        {
            ClosingCharacter = closingCharacter;
            Tag = tag;
        }

        public char ClosingCharacter { get; }

        public string? Tag { get; }

        public List<RuntimeValue> Elements { get; } = new();

        public RuntimeValue CreateValue()
        {
            return ClosingCharacter switch
            {
                ']' => new ArrayValue(Elements),
                '}' => SExpressionValue.FromList(Elements),
                _ => new SExpressionValue(Tag ?? throw new InvalidOperationException("Missing constructor tag"), Elements),
            };
        }
    }
}

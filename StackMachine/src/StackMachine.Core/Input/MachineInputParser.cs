using Language.Runtime.Exceptions;
using Language.Runtime.Input;
using Language.Runtime.Values;

namespace StackMachine.Core.Input;

public static class MachineInputParser
{
    public static IReadOnlyList<RuntimeValue> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            return ValueInputReader.Parse(text);
        }
        catch (LanguageRuntimeException exception)
        {
            throw new MachineInputException(exception.Message, exception);
        }
    }
}

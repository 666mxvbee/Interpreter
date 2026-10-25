using Language.Runtime.Exceptions;

namespace StackMachine.Core.Input;

public sealed class MachineInputException : LanguageRuntimeException
{
    public MachineInputException(string message)
        : base(message)
    {
    }

    public MachineInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

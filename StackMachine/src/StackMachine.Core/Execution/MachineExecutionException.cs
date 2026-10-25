using Language.Runtime.Exceptions;

namespace StackMachine.Core.Execution;

public sealed class MachineExecutionException : LanguageRuntimeException
{
    public MachineExecutionException(string message)
        : base(message)
    {
    }

    public MachineExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

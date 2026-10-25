using Language.Runtime.Exceptions;

namespace Interpreter.Runtime.Exceptions;

public sealed class InterpreterRuntimeException : LanguageRuntimeException
{
    public InterpreterRuntimeException(string message)
        : base(message)
    {
    }
}

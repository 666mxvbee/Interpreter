namespace Interpreter.Runtime.Exceptions;

public sealed class InterpreterRuntimeException : Exception
{
    public InterpreterRuntimeException(string message)
        : base(message)
    {
    }
}
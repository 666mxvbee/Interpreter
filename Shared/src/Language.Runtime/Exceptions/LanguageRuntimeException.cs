namespace Language.Runtime.Exceptions;

public class LanguageRuntimeException : Exception
{
    public LanguageRuntimeException(string message)
        : base(message)
    {
    }

    public LanguageRuntimeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

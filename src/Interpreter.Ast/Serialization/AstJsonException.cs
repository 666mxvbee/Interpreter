namespace Interpreter.Ast.Serialization;

public sealed class AstJsonException : Exception
{
    public AstJsonException(string message)
        : base(message)
    {
    }
}
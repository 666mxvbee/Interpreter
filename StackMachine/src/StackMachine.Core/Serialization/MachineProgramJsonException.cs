namespace StackMachine.Core.Serialization;

public sealed class MachineProgramJsonException : Exception
{
    public MachineProgramJsonException(string message)
        : base(message)
    {
    }
}
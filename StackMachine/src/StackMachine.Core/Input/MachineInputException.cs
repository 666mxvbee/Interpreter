namespace StackMachine.Core.Input;

public sealed class MachineInputException : Exception
{
    public MachineInputException(string message)
        : base(message)
    {
    }
}
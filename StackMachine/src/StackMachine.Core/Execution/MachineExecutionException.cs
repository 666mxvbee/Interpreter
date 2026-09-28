namespace StackMachine.Core.Execution;

public sealed class MachineExecutionException : Exception
{
    public MachineExecutionException(string message)
        : base(message)
    {
    }
}
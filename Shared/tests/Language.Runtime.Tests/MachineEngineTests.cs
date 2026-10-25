using Language.Runtime.Exceptions;
using Language.Runtime.Formatting;
using Language.Runtime.Values;
using StackMachine.Core.Execution;
using StackMachine.Core.Instructions;
using StackMachine.Core.Serialization;
using Xunit;

namespace Language.Runtime.Tests;

public sealed class MachineEngineTests
{
    [Fact]
    public void RepeatedExecutionsDoNotShareMemoryOrStack()
    {
        var engine = new StackMachineEngine();
        Instruction[] store = { new ConstantInstruction(9), new StoreInstruction("x"), new ConstantInstruction(99) };
        Assert.Empty(engine.Execute(store, Array.Empty<RuntimeValue>()));
        Assert.Throws<MachineExecutionException>(() =>
            engine.Execute(new[] { new LoadInstruction("x") }, Array.Empty<RuntimeValue>()));
        Assert.Throws<MachineExecutionException>(() =>
            engine.Execute(new[] { new WriteInstruction() }, Array.Empty<RuntimeValue>()));
        Instruction[] write = { new ConstantInstruction(42), new WriteInstruction() };
        Assert.Equal("42", RuntimeValueFormatter.FormatOutput(engine.Execute(write, Array.Empty<RuntimeValue>())));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConditionalJumpsResolveOnlyTheTakenBranch(bool jumpWhenZero)
    {
        var engine = new StackMachineEngine();
        Instruction jump = jumpWhenZero ? new JumpIfZeroInstruction("missing") : new JumpIfNotZeroInstruction("missing");
        Instruction[] program = { new ReadInstruction(), jump, new ConstantInstruction(42), new WriteInstruction() };
        RuntimeValue[] skipInput = { new IntegerValue(jumpWhenZero ? 1 : 0) };
        Assert.Equal("42", RuntimeValueFormatter.FormatOutput(engine.Execute(program, skipInput)));
        RuntimeValue[] takeInput = { new IntegerValue(jumpWhenZero ? 0 : 1) };
        MachineExecutionException error = Assert.Throws<MachineExecutionException>(() => engine.Execute(program, takeInput));
        Assert.Contains("instruction 1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateLabelsAreRejectedBeforeExecution()
    {
        Instruction[] program = { new WriteInstruction(), new LabelInstruction("L"), new LabelInstruction("L") };
        var engine = new StackMachineEngine();
        MachineExecutionException error = Assert.Throws<MachineExecutionException>(() =>
            engine.Execute(program, Array.Empty<RuntimeValue>()));
        Assert.Contains("Duplicate label", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StackErrorsContainTheInstructionAndPreserveTheirCause()
    {
        Instruction[] program = { new ConstantInstruction(1), new BinaryOperationInstruction(BinaryOperator.Add) };
        var engine = new StackMachineEngine();
        MachineExecutionException error = Assert.Throws<MachineExecutionException>(() =>
            engine.Execute(program, Array.Empty<RuntimeValue>()));
        Assert.Contains("instruction 1", error.Message, StringComparison.Ordinal);
        Assert.IsType<LanguageRuntimeException>(error.InnerException);
    }

    [Fact]
    public void CyclicOutputIsReportedAtItsWriteInstruction()
    {
        var array = new ArrayValue(new[] { new IntegerValue(1) });
        array.SetElement(0, array);
        var engine = new StackMachineEngine();
        Instruction[] program = { new ReadInstruction(), new WriteInstruction() };
        MachineExecutionException error = Assert.Throws<MachineExecutionException>(() => engine.Execute(program, new[] { array }));
        Assert.Contains("cyclic", error.Message, StringComparison.Ordinal);
        Assert.Contains("instruction 1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AggregateCreationPreservesOrderAndSupportsZeroArity()
    {
        var program = MachineProgramJsonParser.Parse(
            "[{\"CONST\":1},{\"CONST\":2},{\"ARRAY\":2},\"WRITE\",{\"ARRAY\":0},\"WRITE\",{\"SEXP\":[\"Nil\",0]},\"WRITE\"]");
        var engine = new StackMachineEngine();
        Assert.Equal("[1, 2], [], Nil", RuntimeValueFormatter.FormatOutput(engine.Execute(program, Array.Empty<RuntimeValue>())));
    }

    [Theory]
    [InlineData("[{\"ARRAY\":-1}]")]
    [InlineData("[{\"ARRAY\":1.5}]")]
    [InlineData("[{\"SEXP\":[\"Foo\",-1]}]")]
    [InlineData("[{\"SEXP\":[\"Foo\"]}]")]
    [InlineData("[{\"SEXP\":[\"\",0]}]")]
    [InlineData("[{\"SEXP\":[\"1Foo\",0]}]")]
    [InlineData("[{\"STRING\":1}]")]
    [InlineData("[{\"CONST\":2147483648}]")]
    [InlineData("[{\"LD\":\"x\",\"ST\":\"y\"}]")]
    [InlineData("[\"UNKNOWN\"]")]
    public void InvalidInstructionShapesAreRejected(string json)
    {
        Assert.Throws<MachineProgramJsonException>(() => MachineProgramJsonParser.Parse(json));
    }

    [Fact]
    public void OversizedProgrammaticCountsDoNotAllocateWithoutOperands()
    {
        var engine = new StackMachineEngine();
        Assert.Throws<MachineExecutionException>(() =>
            engine.Execute(new[] { new ArrayInstruction(int.MaxValue) }, Array.Empty<RuntimeValue>()));
        Assert.Throws<MachineExecutionException>(() =>
            engine.Execute(new[] { new ArrayInstruction(-1) }, Array.Empty<RuntimeValue>()));
    }
}

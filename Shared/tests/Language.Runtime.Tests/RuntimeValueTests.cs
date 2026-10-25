using Language.Runtime.Arithmetic;
using Language.Runtime.Exceptions;
using Language.Runtime.Formatting;
using Language.Runtime.Input;
using Language.Runtime.Values;
using Xunit;

namespace Language.Runtime.Tests;

public sealed class RuntimeValueTests
{
    [Theory]
    [InlineData("+", int.MaxValue, 1, int.MinValue)]
    [InlineData("-", int.MinValue, 1, int.MaxValue)]
    [InlineData("*", 1073741824, 4, 0)]
    [InlineData("/", int.MinValue, -1, int.MinValue)]
    [InlineData("%", int.MinValue, -1, 0)]
    [InlineData("/", -7, 2, -3)]
    [InlineData("%", -7, 2, -1)]
    public void ArithmeticUsesInt32(string operation, int left, int right, int expected)
    {
        RuntimeValue result = BinaryOperations.Evaluate(operation, new IntegerValue(left), new IntegerValue(right));
        Assert.Equal(expected, result.AsInteger());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("%")]
    public void ZeroDivisorIsALanguageError(string operation)
    {
        Assert.Throws<LanguageRuntimeException>(() =>
            BinaryOperations.Evaluate(operation, new IntegerValue(1), new IntegerValue(0)));
    }

    [Theory]
    [InlineData("&&", 0)]
    [InlineData("!!", 1)]
    public void LogicValidatesBothOperands(string operation, int left)
    {
        Assert.Throws<LanguageRuntimeException>(() =>
            BinaryOperations.Evaluate(operation, new IntegerValue(left), new StringValue("x")));
    }

    [Fact]
    public void StringsExposeMutableUtf8Bytes()
    {
        var text = new StringValue("Я");
        RuntimeValue alias = text;
        Assert.Equal(2, text.GetLength());
        Assert.Equal(208, text.GetElement(0).AsInteger());
        Assert.Equal(175, text.GetElement(1).AsInteger());
        alias.SetElement(0, new IntegerValue(209));
        Assert.Equal(209, text.GetElement(0).AsInteger());
        Assert.Throws<LanguageRuntimeException>(() => text.SetElement(0, new IntegerValue(256)));
    }

    [Theory]
    [InlineData("[]", "[]")]
    [InlineData("[ ]", "[]")]
    [InlineData("{}", "{}")]
    [InlineData("\"\"", "\"\"")]
    [InlineData("Cons(1, Nil), {2, 3}", "Cons (1, Nil), {2, 3}")]
    [InlineData("Nil 42", "Nil, 42")]
    [InlineData("1 -2 +3", "1, -2, 3")]
    [InlineData("[1, [], Foo(\"x\")]", "[1, [], Foo (\"x\")]")]
    public void InputSupportsStructuredValues(string input, string expected)
    {
        Assert.Equal(expected, RuntimeValueFormatter.FormatOutput(ValueInputReader.Parse(input)));
    }

    [Theory]
    [InlineData("[1,]")]
    [InlineData("[1,,2]")]
    [InlineData("1,,2")]
    [InlineData("1,")]
    [InlineData("[1 2]")]
    [InlineData("[1]junk")]
    [InlineData("\"x\"junk")]
    [InlineData("2147483648")]
    [InlineData("Foo(1,)")]
    [InlineData("\"bad\\q\"")]
    public void InvalidInputIsALanguageError(string input)
    {
        Assert.Throws<LanguageRuntimeException>(() => ValueInputReader.Parse(input));
    }

    [Fact]
    public void ArraysKeepAliasesButOwnTheirStorage()
    {
        RuntimeValue[] source = { new IntegerValue(1) };
        var array = new ArrayValue(source);
        source[0] = new IntegerValue(2);
        RuntimeValue alias = array;
        Assert.Equal(1, array.GetElement(0).AsInteger());
        alias.SetElement(0, new StringValue("changed"));
        Assert.Equal("[\"changed\"]", RuntimeValueFormatter.Format(array));
    }

    [Fact]
    public void SharedSubarraysAreNotCycles()
    {
        var child = new ArrayValue(new[] { new IntegerValue(1) });
        var parent = new ArrayValue(new[] { child, child });
        Assert.Equal("[[1], [1]]", RuntimeValueFormatter.Format(parent));
        Assert.Equal(0, RuntimeValueComparer.Compare(parent, parent));
    }

    [Fact]
    public void FormattingAndComparisonRejectCycles()
    {
        var array = new ArrayValue(new[] { new IntegerValue(0) });
        array.SetElement(0, array);
        Assert.Throws<LanguageRuntimeException>(() => RuntimeValueFormatter.Format(array));
        Assert.Throws<LanguageRuntimeException>(() => RuntimeValueComparer.Compare(array, array));

        var list = SExpressionValue.FromList(new[] { new IntegerValue(1) });
        list.SetElement(1, list);
        Assert.Throws<LanguageRuntimeException>(() => RuntimeValueFormatter.Format(list));
    }

    [Fact]
    public void AListMayShareItsTailWithItsHead()
    {
        var tail = SExpressionValue.FromList(new[] { new IntegerValue(1) });
        var list = new SExpressionValue("cons", new RuntimeValue[] { tail, tail });
        Assert.Equal("{{1}, 1}", RuntimeValueFormatter.Format(list));
    }

    [Fact]
    public void ComparisonsFollowReferenceTypeRules()
    {
        var shorter = new ArrayValue(new[] { new IntegerValue(9) });
        var longer = new ArrayValue(new[] { new IntegerValue(1), new IntegerValue(2) });
        Assert.True(RuntimeValueComparer.Compare(shorter, longer) < 0);
        var constructor = new SExpressionValue("Nil", Array.Empty<RuntimeValue>());
        Assert.Throws<LanguageRuntimeException>(() => RuntimeValueComparer.Compare(constructor, constructor));
        Assert.Throws<LanguageRuntimeException>(() => RuntimeValueComparer.Compare(new IntegerValue(1), new StringValue("1")));
    }

    [Fact]
    public void DeepInputAndOutputDoNotUseTheHostStack()
    {
        const int depth = 3000;
        string input = new string('[', depth) + "1" + new string(']', depth);
        RuntimeValue value = Assert.Single(ValueInputReader.Parse(input));
        Assert.Equal(input, RuntimeValueFormatter.Format(value));
        Assert.Equal(0, RuntimeValueComparer.Compare(value, value));
    }

    [Fact]
    public void ExcessiveInputDepthIsALanguageError()
    {
        string input = new string('[', RuntimeLimits.MaximumNestingDepth + 1) + "1";
        Assert.Throws<LanguageRuntimeException>(() => ValueInputReader.Parse(input));
    }
}

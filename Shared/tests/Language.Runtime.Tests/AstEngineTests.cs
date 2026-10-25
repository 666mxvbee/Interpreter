using Interpreter.Ast.Expressions;
using Interpreter.Ast.Serialization;
using Interpreter.Ast.Statements;
using Interpreter.Runtime;
using Language.Runtime.Exceptions;
using System.Text.Json;
using Xunit;

namespace Language.Runtime.Tests;

public sealed class AstEngineTests
{
    [Theory]
    [InlineData("[1,]")]
    [InlineData("1,")]
    [InlineData("1,,2")]
    public void UnusedMalformedInputIsRejectedBeforeExecution(string text)
    {
        using var input = new StringReader(text);
        using var output = new StringWriter();
        Assert.ThrowsAny<LanguageRuntimeException>(() =>
        {
            var engine = new InterpreterEngine(input, output);
            engine.Execute(new SkipStatement());
        });
        Assert.Empty(output.ToString());
    }

    [Fact]
    public void LongStatementSequencesDoNotUseTheHostStack()
    {
        Statement program = new WriteStatement(new ConstantExpression(42));

        for (int index = 0; index < 20000; index++)
        {
            program = new SequenceStatement(new SkipStatement(), program);
        }

        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        var engine = new InterpreterEngine(input, output);
        engine.Execute(program);
        Assert.Equal("42" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ExcessiveProgrammaticExpressionDepthIsControlled()
    {
        Expression expression = new ConstantExpression(1);

        for (int index = 0; index <= RuntimeLimits.MaximumNestingDepth; index++)
        {
            expression = new LengthExpression(expression);
        }

        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        var engine = new InterpreterEngine(input, output);
        Assert.ThrowsAny<LanguageRuntimeException>(() => engine.Execute(new WriteStatement(expression)));
        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData("{\"write\":{\"sexp\":\"Foo\"}}")]
    [InlineData("{\"write\":{\"sexp\":\"\",\"args\":[]}}")]
    [InlineData("{\"write\":{\"sexp\":\"Foo\",\"args\":1}}")]
    [InlineData("{\"write\":{\"sexp\":\"Foo\",\"args\":[1]}}")]
    [InlineData("{\"write\":{\"const\":2147483648}}")]
    public void InvalidAstShapesHaveAJsonPath(string json)
    {
        AstJsonException error = Assert.Throws<AstJsonException>(() => AstJsonParser.Parse(json));
        Assert.Contains("$", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonDepthIsRejectedBeforeRecursiveParsing()
    {
        string json = "{\"write\":" + string.Concat(Enumerable.Repeat("{\"length\":", 1000))
            + "{\"const\":1}" + new string('}', 1001);
        Assert.ThrowsAny<JsonException>(() => AstJsonParser.Parse(json));
    }

    [Theory]
    [InlineData("{\"write\":{\"binop\":\"+\",\"left\":{\"var\":\"missing\"},\"right\":{\"binop\":\"/\",\"left\":{\"const\":1},\"right\":{\"const\":0}}}}")]
    [InlineData("{\"set\":{\"dst\":{\"var\":\"missing\"},\"idx\":{\"const\":0},\"value\":{\"binop\":\"/\",\"left\":{\"const\":1},\"right\":{\"const\":0}}}}")]
    public void EvaluationOrderMatchesTheAstReference(string json)
    {
        var program = AstJsonParser.Parse(json);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        var engine = new InterpreterEngine(input, output);
        LanguageRuntimeException error = Assert.ThrowsAny<LanguageRuntimeException>(() => engine.Execute(program));
        Assert.Contains("Division by zero", error.Message, StringComparison.Ordinal);
        Assert.Empty(output.ToString());
    }
}

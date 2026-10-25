using Interpreter.Ast.Statements;
using Interpreter.Runtime.Evaluation;
using Interpreter.Runtime.Exceptions;
using Interpreter.Runtime.State;
using Language.Runtime.Formatting;
using Language.Runtime.Input;
using Language.Runtime.Values;

namespace Interpreter.Runtime;

public sealed class InterpreterEngine
{
    private readonly RuntimeState _state;
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly Queue<RuntimeValue> _input;
    private readonly TextWriter _output;

    public InterpreterEngine(TextReader input, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        _state = new RuntimeState();
        _expressionEvaluator = new ExpressionEvaluator(_state);
        _input = new Queue<RuntimeValue>(ValueInputReader.Parse(input.ReadToEnd()));
        _output = output;
    }

    public void Execute(Statement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var pending = new Stack<Statement>();
        var output = new List<RuntimeValue>();
        pending.Push(statement);

        while (pending.TryPop(out Statement? current))
        {
            switch (current)
            {
                case SkipStatement:
                    break;
                case SequenceStatement sequence:
                    pending.Push(sequence.Right);
                    pending.Push(sequence.Left);
                    break;
                case AssignmentStatement assignment:
                    _state.SetValue(assignment.Destination, _expressionEvaluator.Evaluate(assignment.Source));
                    break;
                case SetStatement set:
                    ExecuteSet(set);
                    break;
                case ReadStatement read:
                    if (!_input.TryDequeue(out RuntimeValue? value))
                    {
                        throw new InterpreterRuntimeException("Input ended before a value could be read");
                    }

                    _state.SetValue(read.Destination, value);
                    break;
                case WriteStatement write:
                    output.Add(_expressionEvaluator.Evaluate(write.Value));
                    break;
                case IfStatement conditional:
                    pending.Push(_expressionEvaluator.Evaluate(conditional.Condition).AsInteger() != 0
                        ? conditional.ThenBranch
                        : conditional.ElseBranch);
                    break;
                case WhileStatement loop:
                    if (_expressionEvaluator.Evaluate(loop.Condition).AsInteger() != 0)
                    {
                        pending.Push(loop);
                        pending.Push(loop.Body);
                    }

                    break;
                case DoWhileStatement loop:
                    pending.Push(new WhileStatement(loop.Condition, loop.Body));
                    pending.Push(loop.Body);
                    break;
                default:
                    throw new InterpreterRuntimeException($"Unsupported statement type {current.GetType().Name}");
            }
        }

        if (output.Count > 0)
        {
            string formatted = RuntimeValueFormatter.FormatOutput(output);
            _output.WriteLine(formatted);
        }
    }

    private void ExecuteSet(SetStatement statement)
    {
        RuntimeValue value = _expressionEvaluator.Evaluate(statement.Value);
        RuntimeValue index = _expressionEvaluator.Evaluate(statement.Index);
        RuntimeValue destination = _expressionEvaluator.Evaluate(statement.Destination);
        destination.SetElement(index.AsInteger(), value);
    }
}

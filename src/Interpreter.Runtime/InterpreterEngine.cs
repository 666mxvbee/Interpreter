using Interpreter.Ast.Statements;
using Interpreter.Runtime.Evaluation;
using Interpreter.Runtime.Exceptions;
using Interpreter.Runtime.State;

namespace Interpreter.Runtime;

public sealed class InterpreterEngine
{
    private readonly RuntimeState _state;
    private readonly ExpressionEvaluator _expressionEvaluator;

    public InterpreterEngine()
    {
        _state = new RuntimeState();
        _expressionEvaluator = new ExpressionEvaluator(_state);
    }

    public void Execute(Statement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        switch (statement)
        {
            case SkipStatement:
                break;

            case SequenceStatement sequence:
                Execute(sequence.Left);
                Execute(sequence.Right);
                break;

            case AssignmentStatement assignment:
                ExecuteAssignment(assignment);
                break;

            case IfStatement conditional:
                ExecuteIf(conditional);
                break;

            case WhileStatement whileStatement:
                ExecuteWhile(whileStatement);
                break;

            case DoWhileStatement doWhileStatement:
                ExecuteDoWhile(doWhileStatement);
                break;

            default:
                throw new InterpreterRuntimeException(
                    $"Unsupported statement type {statement.GetType().Name}");
        }
    }

    private void ExecuteAssignment(AssignmentStatement statement)
    {
        int value = _expressionEvaluator.Evaluate(statement.Source);

        _state.SetValue(statement.Destination, value);
    }

    private void ExecuteIf(IfStatement statement)
    {
        int condition = _expressionEvaluator.Evaluate(statement.Condition);

        if (condition != 0)
        {
            Execute(statement.ThenBranch);
        }
        else
        {
            Execute(statement.ElseBranch);
        }
    }

    private void ExecuteWhile(WhileStatement statement)
    {
        while (_expressionEvaluator.Evaluate(statement.Condition) != 0)
        {
            Execute(statement.Body);
        }
    }

    private void ExecuteDoWhile(DoWhileStatement statement)
    {
        do
        {
            Execute(statement.Body);
        }
        while (_expressionEvaluator.Evaluate(statement.Condition) != 0);
    }
}
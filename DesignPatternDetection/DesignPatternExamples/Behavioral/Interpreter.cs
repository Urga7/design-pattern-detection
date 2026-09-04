using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// AbstractExpression: the interpret operation shared by every rule of the grammar.
[UsedImplicitly]
public abstract class Expression
{
    public abstract int Interpret();
}

// TerminalExpression: a literal of the language, the recursion's base case.
[UsedImplicitly]
public sealed class NumberExpression : Expression
{
    private readonly int _value;

    public NumberExpression(int value) => _value = value;

    public override int Interpret() => _value;
}

// NonterminalExpressions: each grammar rule composes two sub-expressions and combines their results.
[UsedImplicitly]
public sealed class AddExpression : Expression
{
    private readonly Expression _left;
    private readonly Expression _right;

    public AddExpression(Expression left, Expression right)
    {
        _left = left;
        _right = right;
    }

    public override int Interpret() => _left.Interpret() + _right.Interpret();
}

[UsedImplicitly]
public sealed class SubtractExpression : Expression
{
    private readonly Expression _left;
    private readonly Expression _right;

    public SubtractExpression(Expression left, Expression right)
    {
        _left = left;
        _right = right;
    }

    public override int Interpret() => _left.Interpret() - _right.Interpret();
}

using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class InterpreterPatternDetectorTests
{
    private readonly InterpreterPatternDetector _detector = new();

    private const string Arithmetic = """
    namespace Demo;

    public abstract class Expression { public abstract int Interpret(); }

    public sealed class NumberExpression : Expression
    {
        private readonly int _value;
        public NumberExpression(int value) => _value = value;
        public override int Interpret() => _value;
    }

    public sealed class AddExpression : Expression
    {
        private readonly Expression _left;
        private readonly Expression _right;
        public AddExpression(Expression left, Expression right) { _left = left; _right = right; }
        public override int Interpret() => _left.Interpret() + _right.Interpret();
    }

    public sealed class SubtractExpression : Expression
    {
        private readonly Expression _left;
        private readonly Expression _right;
        public SubtractExpression(Expression left, Expression right) { _left = left; _right = right; }
        public override int Interpret() => _left.Interpret() - _right.Interpret();
    }
    """;

    [Fact]
    public void Detects_one_match_per_nonterminal_expression()
    {
        var graph = TestGraph.From(Arithmetic);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal("Expression", match.Bindings["expression"]);
            Assert.Equal("NumberExpression", match.Bindings["terminalExpression"]);
        });
        Assert.Equal(["AddExpression", "SubtractExpression"],
            matches.Select(m => m.Bindings["nonterminalExpression"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_decorator_wrapping_a_single_reference()
    {
        // A Decorator also holds its own abstraction, but exactly one wrapped
        // reference; a grammar rule composes two or more sub-expressions.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class DataSource { public abstract string Read(); }
        public sealed class FileDataSource : DataSource { public override string Read() => "raw"; }

        public sealed class EncryptionDecorator : DataSource
        {
            private readonly DataSource _inner;
            public EncryptionDecorator(DataSource inner) => _inner = inner;
            public override string Read() => "decrypted " + _inner.Read();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_composite_holding_a_collection_of_the_abstraction()
    {
        // A Composite composes children too, but through a collection of the
        // abstraction; a grammar rule has a fixed arity of named operands.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Graphic { public abstract string Draw(); }
        public sealed class Dot : Graphic { public override string Draw() => "."; }

        public sealed class CompoundGraphic : Graphic
        {
            private readonly List<Graphic> _children = new();
            public void Add(Graphic child) => _children.Add(child);
            public override string Draw() => string.Join(" ", _children.Select(child => child.Draw()));
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_context_composing_expressions_from_outside_the_hierarchy()
    {
        // The composing class must be a rule of the grammar itself; a class
        // holding two references without extending the abstraction is plain
        // composition around a strategy-like hierarchy.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Validator { public abstract bool Check(string value); }
        public sealed class NotEmptyValidator : Validator { public override bool Check(string value) => value.Length > 0; }

        public sealed class FormField
        {
            private readonly Validator _first;
            private readonly Validator _second;
            public FormField(Validator first, Validator second) { _first = first; _second = second; }
            public bool Validate(string value) => _first.Check(value) && _second.Check(value);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

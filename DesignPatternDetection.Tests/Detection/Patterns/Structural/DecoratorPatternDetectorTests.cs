using DesignPatternDetection.Detection.Patterns.Structural;

namespace DesignPatternDetection.Tests.Detection.Patterns.Structural;

public class DecoratorPatternDetectorTests
{
    private readonly DecoratorPatternDetector _detector = new();

    [Fact]
    public void Detects_a_decorator_wrapping_its_own_abstraction()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class DataSource { public abstract string Read(); }

        public sealed class FileDataSource : DataSource
        {
            public override string Read() => "raw";
        }

        public sealed class EncryptionDecorator : DataSource
        {
            private readonly DataSource _inner;
            public EncryptionDecorator(DataSource inner) => _inner = inner;
            public override string Read() => $"decrypted({_inner.Read()})";
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("DataSource", match.Bindings["component"]);
        Assert.Equal("FileDataSource", match.Bindings["concreteComponent"]);
        Assert.Equal("EncryptionDecorator", match.Bindings["decorator"]);
    }

    [Fact]
    public void Ignores_an_adapter_wrapping_a_foreign_class()
    {
        // An Adapter wraps a class from outside the Target hierarchy; a
        // Decorator wraps the very abstraction it implements.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(string message); }

        public sealed class EmailNotifier : Notifier
        {
            public override string Send(string message) => message;
        }

        public sealed class LegacyPager { public string Page(string text) => text; }

        public sealed class PagerAdapter : Notifier
        {
            private readonly LegacyPager _pager;
            public PagerAdapter(LegacyPager pager) => _pager = pager;
            public override string Send(string message) => _pager.Page(message);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_wrapper_with_nothing_to_decorate()
    {
        // Without a ConcreteComponent there is no undecorated object for the
        // wrapper to enhance.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class DataSource { public abstract string Read(); }

        public sealed class EncryptionDecorator : DataSource
        {
            private readonly DataSource _inner;
            public EncryptionDecorator(DataSource inner) => _inner = inner;
            public override string Read() => $"decrypted({_inner.Read()})";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_wrapper_that_never_overrides_the_operation()
    {
        // Holding a reference to the abstraction without overriding its
        // operation is composition, not decoration.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class DataSource { public abstract string Read(); }

        public sealed class FileDataSource : DataSource
        {
            public override string Read() => "raw";
        }

        public sealed class DataSourceCache : DataSource
        {
            private readonly DataSource _inner;
            public DataSourceCache(DataSource inner) => _inner = inner;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_an_interpreter_nonterminal_composing_two_references()
    {
        // A NonterminalExpression also extends its abstraction and holds it
        // in fields, but composes two operands; a decorator adds behaviour
        // around exactly one wrapped object.
        var graph = TestGraph.From("""
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
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_wrapper_that_never_forwards_to_the_wrapped_instance()
    {
        // Wrapping without delegating is mere aggregation: a decorator adds
        // behaviour around the wrapped call, so the override must actually
        // forward to the wrapped component.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class DataSource { public abstract string Read(); }

        public sealed class FileDataSource : DataSource
        {
            public override string Read() => "raw";
        }

        public sealed class CachingRegistry : DataSource
        {
            private DataSource _template;
            public override string Read() => "registry";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Detects_a_decorator_whose_abstract_base_holds_the_wrappee()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Command { public abstract string Execute(); }

        public sealed class PlainCommand : Command { public override string Execute() => "plain"; }

        public abstract class DelegatingCommand : Command
        {
            protected readonly Command Inner;
            protected DelegatingCommand(Command inner) => Inner = inner;
        }

        public sealed class TimedCommand : DelegatingCommand
        {
            public TimedCommand(Command inner) : base(inner) { }
            public override string Execute() => Inner.Execute();
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));

        Assert.Equal("Command", match.Bindings["component"]);
        Assert.Equal("PlainCommand", match.Bindings["concreteComponent"]);
        Assert.Equal("DelegatingCommand", match.Bindings["decorator"]);
    }
}

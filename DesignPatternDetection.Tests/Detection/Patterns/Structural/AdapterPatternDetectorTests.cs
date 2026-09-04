using DesignPatternDetection.Detection.Patterns.Structural;

namespace DesignPatternDetection.Tests.Detection.Patterns.Structural;

public class AdapterPatternDetectorTests
{
    private readonly AdapterPatternDetector _detector = new();

    [Fact]
    public void Detects_an_adapter_wrapping_a_foreign_adaptee()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(string message); }

        public sealed class LegacyPager { public string Page(string text) => text; }

        public sealed class PagerAdapter : Notifier
        {
            private readonly LegacyPager _pager;
            public PagerAdapter(LegacyPager pager) => _pager = pager;
            public override string Send(string message) => _pager.Page(message);
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("Notifier", match.Bindings["target"]);
        Assert.Equal("PagerAdapter", match.Bindings["adapter"]);
        Assert.Equal("LegacyPager", match.Bindings["adaptee"]);
    }

    [Fact]
    public void Ignores_a_decorator_that_wraps_its_own_abstraction()
    {
        // A Decorator holds a field of the very type it implements; an Adapter
        // wraps a class from outside the Target hierarchy.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(string message); }

        public sealed class LoggingNotifier : Notifier
        {
            private readonly Notifier _inner;
            public LoggingNotifier(Notifier inner) => _inner = inner;
            public override string Send(string message) => _inner.Send(message);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_wrapper_around_a_subtype_of_its_own_target()
    {
        // Wrapping a concrete sibling from the same hierarchy is proxy- or
        // decorator-shaped, not adaptation of an incompatible class.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(string message); }

        public sealed class EmailNotifier : Notifier
        {
            public override string Send(string message) => message;
        }

        public sealed class RetryingNotifier : Notifier
        {
            private readonly EmailNotifier _inner;
            public RetryingNotifier(EmailNotifier inner) => _inner = inner;
            public override string Send(string message) => _inner.Send(message);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_an_override_whose_fields_are_not_source_declared_classes()
    {
        // Primitive-typed state is not a wrapped Adaptee: the adaptee must be
        // a class or interface declared in the scanned source.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Transport { public abstract string Deliver(); }

        public sealed class Truck : Transport
        {
            private readonly string _route = "north";
            public override string Deliver() => _route;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_concrete_command_whose_abstraction_an_invoker_stores()
    {
        // A ConcreteCommand is adapter-shaped - it extends an abstraction and
        // wraps a foreign class - but an invoker storing that abstraction in
        // a field for later invocation marks the triad as Command.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Command { public abstract string Execute(); }

        public sealed class Light { public string TurnOn() => "on"; }

        public sealed class LightOnCommand : Command
        {
            private readonly Light _light;
            public LightOnCommand(Light light) => _light = light;
            public override string Execute() => _light.TurnOn();
        }

        public sealed class RemoteButton
        {
            private readonly Command _command;
            public RemoteButton(Command command) => _command = command;
            public string Press() => _command.Execute();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_wrapper_manufactured_by_the_class_it_wraps()
    {
        // An Adaptee never creates its own adapter; an aggregate handing out
        // the wrapper that walks it is an Iterator.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class BookIterator
        {
            public abstract bool HasNext();
            public abstract string Next();
        }

        public sealed class ShelfIterator : BookIterator
        {
            private readonly BookShelf _shelf;
            private int _position;

            public ShelfIterator(BookShelf shelf) => _shelf = shelf;

            public override bool HasNext() => _position < _shelf.Count;
            public override string Next() => _shelf.BookAt(_position++);
        }

        public sealed class BookShelf
        {
            private readonly List<string> _books = new();

            public int Count => _books.Count;
            public string BookAt(int index) => _books[index];

            public BookIterator CreateIterator() => new ShelfIterator(this);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Detects_an_adapter_that_wraps_a_foreign_interface()
    {
        // Converting one interface into another is the textbook use of the
        // pattern - log4net's Layout2RawLayoutAdapter wraps ILayout to satisfy
        // IRawLayout - so a source-declared interface counts as an Adaptee.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface IRawLayout { object Format(string source); }

        public interface ILayout { void Format(System.IO.TextWriter writer, string source); }

        public sealed class SimpleLayout : ILayout
        {
            public void Format(System.IO.TextWriter writer, string source) => writer.Write(source);
        }

        public sealed class LayoutToRawLayoutAdapter : IRawLayout
        {
            private readonly ILayout _layout;
            public LayoutToRawLayoutAdapter(ILayout layout) => _layout = layout;

            public object Format(string source)
            {
                var writer = new System.IO.StringWriter();
                _layout.Format(writer, source);
                return writer.ToString();
            }
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));

        Assert.Equal("IRawLayout", match.Bindings["target"]);
        Assert.Equal("LayoutToRawLayoutAdapter", match.Bindings["adapter"]);
        Assert.Equal("ILayout", match.Bindings["adaptee"]);
    }

    [Fact]
    public void Ignores_a_wrapper_holding_two_references_to_the_same_abstraction()
    {
        // Serilog's OptionalInterfaceForwardingSink keeps a sink and a receiver,
        // both ILogEventSink. Two references to one abstraction is an
        // Interpreter nonterminal, so the wrapped reference must be the
        // adapter's only source-declared collaborator.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface IFailureListener { void OnFailure(string reason); }

        public interface ISink { void Emit(string message); }

        public sealed class ForwardingSink : ISink, IFailureListener
        {
            private readonly ISink _sink;
            private readonly ISink _receiver;

            public ForwardingSink(ISink sink, ISink receiver)
            {
                _sink = sink;
                _receiver = receiver;
            }

            public void Emit(string message) => _sink.Emit(message);
            public void OnFailure(string reason) => _receiver.Emit(reason);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_wrapper_that_also_aggregates_its_own_target()
    {
        // NLog's ConditionMethodExpression forwards to one method object but
        // also carries an IList<ConditionExpression> of its operands: a wrapper
        // collecting the abstraction it implements is a Composite or an
        // Interpreter nonterminal, not an Adapter.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Expression { protected abstract object Evaluate(); }

        public interface IEvaluateMethod { object Evaluate(); }

        public sealed class MethodExpression : Expression
        {
            private readonly IEvaluateMethod _method;
            public System.Collections.Generic.IList<Expression> Parameters { get; }

            public MethodExpression(IEvaluateMethod method, System.Collections.Generic.IList<Expression> parameters)
            {
                _method = method;
                Parameters = parameters;
            }

            protected override object Evaluate() => _method.Evaluate();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

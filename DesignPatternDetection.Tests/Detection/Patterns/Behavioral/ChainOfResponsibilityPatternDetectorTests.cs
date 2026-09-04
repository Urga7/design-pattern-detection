using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class ChainOfResponsibilityPatternDetectorTests
{
    private readonly ChainOfResponsibilityPatternDetector _detector = new();

    private const string SupportChain = """
    namespace Demo;

    public abstract class SupportHandler
    {
        protected readonly SupportHandler? Next;
        protected SupportHandler(SupportHandler? next) => Next = next;
        public abstract string Handle(string request);
    }

    public sealed class FirstLevelSupport : SupportHandler
    {
        public FirstLevelSupport(SupportHandler? next) : base(next) { }
        public override string Handle(string request) =>
            request == "password reset" ? "resolved" : Next?.Handle(request) ?? "unhandled";
    }

    public sealed class SecondLevelSupport : SupportHandler
    {
        public SecondLevelSupport(SupportHandler? next) : base(next) { }
        public override string Handle(string request) => "escalated";
    }
    """;

    [Fact]
    public void Detects_one_match_per_concrete_handler()
    {
        var graph = TestGraph.From(SupportChain);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => Assert.Equal("SupportHandler", match.Bindings["handler"]));
        Assert.Equal(["FirstLevelSupport", "SecondLevelSupport"],
            matches.Select(m => m.Bindings["concreteHandler"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_decorator_whose_reference_lives_on_the_subclass()
    {
        // A Decorator also holds a reference typed as its abstraction, but on
        // the wrapper subclass; a chain's successor link sits on the
        // abstraction itself so any handler can stand behind any other.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class DataSource { public abstract string Read(); }
        public sealed class FileDataSource : DataSource { public override string Read() => "raw"; }

        public sealed class EncryptionDecorator : DataSource
        {
            private readonly DataSource _wrapped;
            public EncryptionDecorator(DataSource wrapped) => _wrapped = wrapped;
            public override string Read() => "decrypted " + _wrapped.Read();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Detects_a_chain_whose_link_lives_on_an_abstract_base_of_the_interface()
    {
        // Idiomatic C# splits the abstraction into an interface and an
        // abstract base carrying the successor link; the concrete handlers
        // extend the base and forward through the inherited field.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface IHandler
        {
            object Handle(object request);
        }

        public abstract class AbstractHandler : IHandler
        {
            private IHandler _next;
            public virtual object Handle(object request) => _next != null ? _next.Handle(request) : null;
        }

        public sealed class MonkeyHandler : AbstractHandler
        {
            public override object Handle(object request) => "banana";
        }
        """);

        var matches = _detector.Detect(graph);

        Assert.Equal(["MonkeyHandler"], matches.Select(m => m.Bindings["concreteHandler"]));
        Assert.All(matches, match => Assert.Equal("IHandler", match.Bindings["handler"]));
    }

    [Fact]
    public void Ignores_a_decorator_with_a_base_wrapper_class()
    {
        // A base Decorator class extending the Component and wrapping it looks
        // exactly like a chain's linking base - but a plain ConcreteComponent
        // implementing the abstraction without the link gives it away: chains
        // have no undecorated component.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Component { public abstract string Operation(); }
        public sealed class ConcreteComponent : Component { public override string Operation() => "plain"; }

        public abstract class Decorator : Component
        {
            private readonly Component _component;
            public Decorator(Component component) => _component = component;
            public override string Operation() => _component.Operation();
        }

        public sealed class ConcreteDecoratorA : Decorator
        {
            public ConcreteDecoratorA(Component component) : base(component) { }
            public override string Operation() => "A(" + base.Operation() + ")";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_static_self_reference_like_a_default_instance()
    {
        // A static member typed as the declaring class is a Singleton-style
        // shared instance, not a successor link in a chain.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Logger
        {
            private static Logger? _default;
            public static void SetDefault(Logger logger) => _default = logger;
            public abstract string Log(string message);
        }

        public sealed class ConsoleLogger : Logger
        {
            public override string Log(string message) => message;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_strategy_context_that_wraps_the_abstraction_from_outside()
    {
        // A Strategy context also holds the abstraction, but from outside the
        // hierarchy; the chain's link is the abstraction referencing itself.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class SortStrategy { public abstract string Sort(string items); }
        public sealed class QuickSort : SortStrategy { public override string Sort(string items) => "quick"; }

        public sealed class Sorter
        {
            private readonly SortStrategy _strategy;
            public Sorter(SortStrategy strategy) => _strategy = strategy;
            public string Sort(string items) => _strategy.Sort(items);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Detects_a_successor_exposed_as_a_property()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Filter
        {
            public Filter? Next { get; set; }
            public abstract string Decide(string request);
            public string Continue(string request) => Next!.Decide(request);
        }

        public sealed class LevelFilter : Filter { public override string Decide(string request) => "accept"; }

        public sealed class NameFilter : Filter { public override string Decide(string request) => "deny"; }
        """);

        var matches = _detector.Detect(graph);

        Assert.All(matches, match => Assert.Equal("Filter", match.Bindings["handler"]));
        Assert.Equal(["LevelFilter", "NameFilter"],
            matches.Select(m => m.Bindings["concreteHandler"]).OrderBy(name => name));
    }
}

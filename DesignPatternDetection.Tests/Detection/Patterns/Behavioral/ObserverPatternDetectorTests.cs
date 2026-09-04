using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class ObserverPatternDetectorTests
{
    private readonly ObserverPatternDetector _detector = new();

    private const string NewsFeed = """
    namespace Demo;

    public abstract class Subscriber { public abstract string Update(string news); }
    public sealed class EmailSubscriber : Subscriber { public override string Update(string news) => "email"; }
    public sealed class SmsSubscriber : Subscriber { public override string Update(string news) => "sms"; }

    public sealed class NewsAgency
    {
        private readonly List<Subscriber> _subscribers = new();
        public void Subscribe(Subscriber subscriber) => _subscribers.Add(subscriber);
        public string Publish(string news) => string.Join("|", _subscribers.Select(s => s.Update(news)));
    }
    """;

    [Fact]
    public void Detects_one_match_per_concrete_observer()
    {
        var graph = TestGraph.From(NewsFeed);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal("NewsAgency", match.Bindings["subject"]);
            Assert.Equal("Subscriber", match.Bindings["observer"]);
        });
        Assert.Equal(["EmailSubscriber", "SmsSubscriber"],
            matches.Select(m => m.Bindings["concreteObserver"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_composite_that_collects_its_own_abstraction()
    {
        // A Composite also holds a collection of an abstraction, but of the
        // very one it extends; an Observer's subject stands outside the
        // hierarchy it notifies.
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
    public void Ignores_a_flyweight_pool_of_a_concrete_class()
    {
        // A Flyweight factory also holds a collection of a source class, but
        // one with no abstract update operation to notify.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class TreeType
        {
            private readonly string _name;
            public TreeType(string name) => _name = name;
            public string Draw(int x, int y) => _name;
        }

        public sealed class TreeFactory
        {
            private readonly Dictionary<string, TreeType> _pool = new();

            public TreeType GetTreeType(string name)
            {
                if (!_pool.TryGetValue(name, out var type))
                {
                    type = new TreeType(name);
                    _pool[name] = type;
                }

                return type;
            }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_registry_that_creates_the_instances_it_collects()
    {
        // Observers subscribe from outside; a holder that instantiates its
        // own elements is a factory over a pool, not an observed subject.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Handler { public abstract string Handle(); }
        public sealed class DefaultHandler : Handler { public override string Handle() => "default"; }

        public sealed class HandlerRegistry
        {
            private readonly List<Handler> _handlers = new();

            public Handler CreateDefault()
            {
                var handler = new DefaultHandler();
                _handlers.Add(handler);
                return handler;
            }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_caretaker_with_no_registration_seam()
    {
        // A Memento caretaker also collects a foreign abstraction and calls
        // into it, but observers subscribe from outside: the subject must
        // take the abstraction as a parameter somewhere.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface IMemento { string GetName(); }

        public sealed class ConcreteMemento : IMemento
        {
            public string GetName() => "snapshot";
        }

        public sealed class Originator
        {
            public IMemento Save() => new ConcreteMemento();
        }

        public sealed class Caretaker
        {
            private readonly List<IMemento> _mementos = new();
            private Originator _originator;

            public void Backup() => _mementos.Add(_originator.Save());
            public string ShowHistory() => _mementos[0].GetName();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

using DesignPatternDetection.Detection.Patterns.Structural;

namespace DesignPatternDetection.Tests.Detection.Patterns.Structural;

public class FlyweightPatternDetectorTests
{
    private readonly FlyweightPatternDetector _detector = new();

    [Fact]
    public void Detects_a_factory_pooling_shared_flyweights()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class TreeType
        {
            private readonly string _name;
            public TreeType(string name) => _name = name;
            public string Draw(int x, int y) => $"{_name} at ({x}, {y})";
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

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("TreeFactory", match.Bindings["flyweightFactory"]);
        Assert.Equal("TreeType", match.Bindings["flyweight"]);
    }

    [Fact]
    public void Detects_a_pool_of_constructor_initialized_flyweights_in_nested_generics()
    {
        // Idiomatic variant (the RefactoringGuru shape): the intrinsic state
        // is a plain private field assigned only in the constructor, and the
        // pool nests the flyweight inside a tuple.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Car { }

        public sealed class Flyweight
        {
            private Car _sharedState;
            public Flyweight(Car sharedState) => _sharedState = sharedState;
        }

        public sealed class FlyweightFactory
        {
            private readonly List<Tuple<Flyweight, string>> _flyweights = new();

            public Flyweight GetFlyweight(Car sharedState)
            {
                var flyweight = new Flyweight(sharedState);
                _flyweights.Add(new Tuple<Flyweight, string>(flyweight, "key"));
                return flyweight;
            }
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("FlyweightFactory", match.Bindings["flyweightFactory"]);
        Assert.Equal("Flyweight", match.Bindings["flyweight"]);
    }

    [Fact]
    public void Ignores_a_constructor_assigned_field_that_a_method_later_mutates()
    {
        // Constructor assignment alone is not immutability - a method
        // reassigning the same field makes the pooled object mutable.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Session
        {
            private string _user;
            public Session(string user) => _user = user;
            public void Reassign(string user) => _user = user;
        }

        public sealed class SessionCache
        {
            private readonly Dictionary<string, Session> _sessions = new();

            public Session GetSession(string key)
            {
                if (!_sessions.TryGetValue(key, out var session))
                {
                    session = new Session(key);
                    _sessions[key] = session;
                }

                return session;
            }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_registry_that_only_stores_externally_created_objects()
    {
        // Without the get-or-create shape - the pooling method instantiating
        // the flyweight itself - this is a plain lookup table.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class TreeType
        {
            private readonly string _name;
            public TreeType(string name) => _name = name;
        }

        public sealed class TreeRegistry
        {
            private readonly Dictionary<string, TreeType> _entries = new();
            public void Register(string name, TreeType type) => _entries[name] = type;
            public TreeType Lookup(string name) => _entries[name];
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_pool_of_mutable_objects()
    {
        // Sharing only works when the intrinsic state is immutable; a cache
        // of mutable objects is not a Flyweight.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Session
        {
            private string _user = "";
            public void Assign(string user) => _user = user;
        }

        public sealed class SessionCache
        {
            private readonly Dictionary<string, Session> _sessions = new();

            public Session GetSession(string key)
            {
                if (!_sessions.TryGetValue(key, out var session))
                {
                    session = new Session();
                    _sessions[key] = session;
                }

                return session;
            }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_composite_shaped_holder_inside_the_flyweight_hierarchy()
    {
        // A collection of the own abstraction belongs to Composite; the
        // flyweight factory stands outside the pooled class's hierarchy.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Widget
        {
            private readonly string _style = "flat";
        }

        public sealed class Panel : Widget
        {
            private readonly List<Widget> _children = [];
            public Widget Grow() => new Widget();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

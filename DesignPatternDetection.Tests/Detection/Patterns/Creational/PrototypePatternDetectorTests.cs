using DesignPatternDetection.Detection.Patterns.Creational;

namespace DesignPatternDetection.Tests.Detection.Patterns.Creational;

public class PrototypePatternDetectorTests
{
    private readonly PrototypePatternDetector _detector = new();

    private const string Shapes = """
    namespace Demo;

    public abstract class Shape
    {
        public abstract Shape Clone();
    }

    public sealed class Circle : Shape
    {
        public Circle() { }
        private Circle(Circle source) { }
        public override Shape Clone() => new Circle(this);
    }

    public sealed class Rectangle : Shape
    {
        public Rectangle() { }
        private Rectangle(Rectangle source) { }
        public override Shape Clone() => new Rectangle(this);
    }
    """;

    [Fact]
    public void Detects_one_match_per_concrete_prototype()
    {
        var graph = TestGraph.From(Shapes);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => Assert.Equal("Shape", match.Bindings["prototype"]));
        Assert.Equal(["Circle", "Rectangle"],
            matches.Select(m => m.Bindings["concretePrototype"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_clone_that_does_not_instantiate_a_copy()
    {
        // The override returns the prototype type but hands back the original
        // instead of building a fresh copy, so nothing is instantiated.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Shape
        {
            public abstract Shape Clone();
        }

        public sealed class Circle : Shape
        {
            public override Shape Clone() => this;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_factory_method_whose_creator_returns_a_different_type()
    {
        // A Factory Method's creation method lives on a separate Creator and
        // returns a distinct Product type, so the self-typed clone signature -
        // a method returning its own declaring type - is absent.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Transport { }
        public sealed class Truck : Transport { }

        public abstract class Logistics
        {
            public abstract Transport Create();
        }

        public sealed class RoadLogistics : Logistics
        {
            public override Transport Create() => new Truck();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Detects_a_concrete_class_cloning_itself_with_memberwise_clone()
    {
        // Idiomatic C#: no abstraction at all, just a class copying itself
        // through MemberwiseClone (the RefactoringGuru shape).
        var graph = TestGraph.From("""
        namespace Demo;

        public class Person
        {
            public int Age;
            public Person ShallowCopy() => (Person)this.MemberwiseClone();
            public Person DeepCopy()
            {
                Person clone = (Person)this.MemberwiseClone();
                clone.Age = Age;
                return clone;
            }
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("Person", match.Bindings["prototype"]);
        Assert.Equal("Person", match.Bindings["concretePrototype"]);
    }

    [Fact]
    public void Detects_an_icloneable_clone_returning_object()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public class Document : ICloneable
        {
            public object Clone() => this.MemberwiseClone();
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("Document", match.Bindings["prototype"]);
        Assert.Equal("Document", match.Bindings["concretePrototype"]);
    }

    [Fact]
    public void Ignores_a_singleton_returning_its_cached_static_instance()
    {
        // A Singleton's accessor is also self-typed and self-instantiating,
        // but it is static - the class hands out the one instance, it does
        // not let instances copy themselves.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Singleton
        {
            private static Singleton _instance;
            private Singleton() { }
            public static Singleton GetInstance()
            {
                if (_instance == null)
                    _instance = new Singleton();
                return _instance;
            }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_fluent_method_returning_itself_without_copying()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class QueryBuilder
        {
            public QueryBuilder WithFilter() { return this; }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

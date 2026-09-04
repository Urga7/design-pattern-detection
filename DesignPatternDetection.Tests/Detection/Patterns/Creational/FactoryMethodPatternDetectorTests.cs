using DesignPatternDetection.Detection.Patterns.Creational;

namespace DesignPatternDetection.Tests.Detection.Patterns.Creational;

public class FactoryMethodPatternDetectorTests
{
    private readonly FactoryMethodPatternDetector _detector = new();

    [Fact]
    public void Detects_an_overridden_factory_method_that_builds_a_concrete_product()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Transport { public abstract string Deliver(); }
        public sealed class Truck : Transport { public override string Deliver() => "land"; }
        public sealed class Ship : Transport { public override string Deliver() => "sea"; }

        public abstract class Logistics { public abstract Transport CreateTransport(); }
        public sealed class RoadLogistics : Logistics
        {
            public override Transport CreateTransport() => new Truck();
        }
        public sealed class SeaLogistics : Logistics
        {
            public override Transport CreateTransport() => new Ship();
        }
        """);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal("Logistics", match.Bindings["creator"]);
            Assert.Equal("Transport", match.Bindings["product"]);
        });
        Assert.Equal(["RoadLogistics", "SeaLogistics"],
            matches.Select(m => m.Bindings["concreteCreator"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_an_override_that_returns_a_product_it_does_not_instantiate()
    {
        // The subclass overrides the factory method but returns a cached
        // instance instead of a concrete subtype of the product, so the
        // "instantiates a concrete product" half of the rule is unmet.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Transport { public abstract string Deliver(); }
        public sealed class Truck : Transport { public override string Deliver() => "land"; }

        public abstract class Logistics
        {
            public abstract Transport CreateTransport();
        }
        public sealed class RoadLogistics : Logistics
        {
            private readonly Transport _cached = new Truck();
            public override Transport CreateTransport() => _cached;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_an_abstract_factory_that_creates_a_family_of_products()
    {
        // A creator declaring abstract factory methods for several distinct
        // products assembles a family - that is an Abstract Factory, and each
        // individual create method must not count as a Factory Method.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Button { public abstract string Render(); }
        public sealed class WinButton : Button { public override string Render() => "win button"; }

        public abstract class Checkbox { public abstract string Render(); }
        public sealed class WinCheckbox : Checkbox { public override string Render() => "win checkbox"; }

        public abstract class GuiFactory
        {
            public abstract Button CreateButton();
            public abstract Checkbox CreateCheckbox();
        }

        public sealed class WinFactory : GuiFactory
        {
            public override Button CreateButton() => new WinButton();
            public override Checkbox CreateCheckbox() => new WinCheckbox();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_prototype_whose_clone_returns_its_own_hierarchy()
    {
        // A factory method returning the creator's own abstract type is a
        // Prototype clone: the "product" is the creator hierarchy itself.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Shape
        {
            public abstract Shape Clone();
        }

        public sealed class Circle : Shape
        {
            public override Shape Clone() => new Circle();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_class_with_no_creator_hierarchy()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public sealed class PlainService
        {
            public string DoWork() => "done";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Detects_a_product_built_two_levels_below_the_declared_product()
    {
        // Real hierarchies put an abstract base between the abstraction the
        // factory method declares and the class it constructs - Castle.Core has
        // ILogger <- LevelFilteredLogger <- ConsoleLogger - so the subtype step
        // is transitive.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface ILogger { void Write(string message); }

        public abstract class LevelFilteredLogger : ILogger
        {
            public void Write(string message) => Log(message);
            protected abstract void Log(string message);
        }

        public sealed class ConsoleLogger : LevelFilteredLogger
        {
            protected override void Log(string message) { }
        }

        public abstract class LoggerFactory { public abstract ILogger Create(string name); }

        public sealed class ConsoleLoggerFactory : LoggerFactory
        {
            public override ILogger Create(string name) => new ConsoleLogger();
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));

        Assert.Equal("LoggerFactory", match.Bindings["creator"]);
        Assert.Equal("ILogger", match.Bindings["product"]);
        Assert.Equal("ConsoleLoggerFactory", match.Bindings["concreteCreator"]);
        Assert.Equal("ConsoleLogger", match.Bindings["concreteProduct"]);
    }

    [Fact]
    public void Ignores_an_aggregate_whose_product_holds_a_reference_back_to_it()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Cursor { public abstract bool MoveNext(); }

        public abstract class Collection { public abstract Cursor Open(); }

        public sealed class WordCollection : Collection
        {
            public override Cursor Open() => new WordCursor(this);
        }

        public sealed class WordCursor : Cursor
        {
            private readonly WordCollection _collection;
            public WordCursor(WordCollection collection) => _collection = collection;
            public override bool MoveNext() => false;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

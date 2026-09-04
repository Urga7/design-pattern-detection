using DesignPatternDetection.Detection.Patterns.Creational;

namespace DesignPatternDetection.Tests.Detection.Patterns.Creational;

public class BuilderPatternDetectorTests
{
    private readonly BuilderPatternDetector _detector = new();

    private const string CarBuilders = """
    namespace Demo;

    public sealed class Car { }

    public abstract class CarBuilder
    {
        public abstract void SetSeats(int number);
        public abstract void SetEngine(string engine);
        public abstract Car Build();
    }

    public sealed class SportsCarBuilder : CarBuilder
    {
        private int _seats;
        private string _engine = "";
        public override void SetSeats(int number) => _seats = number;
        public override void SetEngine(string engine) => _engine = engine;
        public override Car Build() => new Car();
    }

    public sealed class SuvBuilder : CarBuilder
    {
        private int _seats;
        private string _engine = "";
        public override void SetSeats(int number) => _seats = number;
        public override void SetEngine(string engine) => _engine = engine;
        public override Car Build() => new Car();
    }
    """;

    [Fact]
    public void Detects_one_match_per_concrete_builder()
    {
        var graph = TestGraph.From(CarBuilders);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal("CarBuilder", match.Bindings["builder"]);
            Assert.Equal("Car", match.Bindings["product"]);
        });
        Assert.Equal(["SportsCarBuilder", "SuvBuilder"],
            matches.Select(m => m.Bindings["concreteBuilder"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_concrete_builder_that_returns_a_prebuilt_product_it_does_not_instantiate()
    {
        // The builder assembles nothing itself - Build hands back a cached
        // product, so the "instantiates the product" half of the rule is unmet.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Car { }

        public abstract class CarBuilder
        {
            public abstract void SetSeats(int number);
            public abstract void SetEngine(string engine);
            public abstract Car Build();
        }

        public sealed class PrebuiltCarBuilder : CarBuilder
        {
            private readonly Car _car = new Car();
            public override void SetSeats(int number) { }
            public override void SetEngine(string engine) { }
            public override Car Build() => _car;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_builder_with_a_single_construction_step()
    {
        // With only one abstract step this is a plain Factory Method, not a
        // step-by-step Builder - there is no multi-step construction interface.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Car { }

        public abstract class CarBuilder
        {
            public abstract Car Build();
        }

        public sealed class SportsCarBuilder : CarBuilder
        {
            public override Car Build() => new Car();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Detects_a_builder_that_resets_in_one_method_and_returns_in_another()
    {
        // Idiomatic builders instantiate the product in a reset method and
        // hand it out from a separate, non-overriding result method.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface IBuilder { void BuildPartA(); void BuildPartB(); }

        public sealed class Product { public void Add(string part) { } }

        public sealed class ConcreteBuilder : IBuilder
        {
            private Product _product;
            public void BuildPartA() => _product.Add("A");
            public void BuildPartB() => _product.Add("B");
            public void Reset() => _product = new Product();
            public Product GetProduct() => _product;
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("IBuilder", match.Bindings["builder"]);
        Assert.Equal("ConcreteBuilder", match.Bindings["concreteBuilder"]);
        Assert.Equal("Product", match.Bindings["product"]);
    }
}

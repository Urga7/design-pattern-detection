using DesignPatternDetection.Detection.Patterns.Creational;

namespace DesignPatternDetection.Tests.Detection.Patterns.Creational;

public class AbstractFactoryPatternDetectorTests
{
    private readonly AbstractFactoryPatternDetector _detector = new();

    private const string GuiFactory = """
    namespace Demo;

    public abstract class Button { public abstract string Render(); }
    public abstract class Checkbox { public abstract string Render(); }

    public sealed class WinButton : Button { public override string Render() => "w-b"; }
    public sealed class WinCheckbox : Checkbox { public override string Render() => "w-c"; }
    public sealed class MacButton : Button { public override string Render() => "m-b"; }
    public sealed class MacCheckbox : Checkbox { public override string Render() => "m-c"; }

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

    public sealed class MacFactory : GuiFactory
    {
        public override Button CreateButton() => new MacButton();
        public override Checkbox CreateCheckbox() => new MacCheckbox();
    }
    """;

    [Fact]
    public void Detects_one_match_per_concrete_factory()
    {
        var graph = TestGraph.From(GuiFactory);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => Assert.Equal("GuiFactory", match.Bindings["factory"]));
        Assert.Equal(["MacFactory", "WinFactory"],
            matches.Select(m => m.Bindings["concreteFactory"]).OrderBy(name => name));
    }

    [Fact]
    public void Reports_the_product_family_in_canonical_order()
    {
        var graph = TestGraph.From(GuiFactory);

        var match = _detector.Detect(graph)[0];

        // The STR(?productA) < STR(?productB) filter pins a single, ordered pair
        // so the symmetric duplicate (products swapped) never shows up.
        Assert.Equal("Button", match.Bindings["productA"]);
        Assert.Equal("Checkbox", match.Bindings["productB"]);
    }

    [Fact]
    public void Ignores_a_plain_factory_method_with_a_single_product()
    {
        // One creation method is a Factory Method, not an Abstract Factory:
        // there is no family of related products.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Transport { public abstract string Deliver(); }
        public sealed class Truck : Transport { public override string Deliver() => "land"; }

        public abstract class Logistics { public abstract Transport CreateTransport(); }
        public sealed class RoadLogistics : Logistics
        {
            public override Transport CreateTransport() => new Truck();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_concrete_factory_that_only_builds_part_of_the_family()
    {
        // The factory declares two products, but the concrete factory overrides
        // (and instantiates) only one of them - it does not deliver a full family.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Button { public abstract string Render(); }
        public abstract class Checkbox { public abstract string Render(); }
        public sealed class WinButton : Button { public override string Render() => "w-b"; }

        public abstract class GuiFactory
        {
            public abstract Button CreateButton();
            public abstract Checkbox CreateCheckbox();
        }

        public sealed class WinFactory : GuiFactory
        {
            public override Button CreateButton() => new WinButton();
            public override Checkbox CreateCheckbox() => throw new System.NotImplementedException();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class VisitorPatternDetectorTests
{
    private readonly VisitorPatternDetector _detector = new();

    private const string DocumentExport = """
    namespace Demo;

    public abstract class DocumentVisitor
    {
        public abstract string VisitPlainText(PlainText text);
        public abstract string VisitHyperlink(Hyperlink link);
    }

    public abstract class DocumentPart { public abstract string Accept(DocumentVisitor visitor); }

    public sealed class PlainText : DocumentPart
    {
        public override string Accept(DocumentVisitor visitor) => visitor.VisitPlainText(this);
    }

    public sealed class Hyperlink : DocumentPart
    {
        public override string Accept(DocumentVisitor visitor) => visitor.VisitHyperlink(this);
    }

    public sealed class HtmlExportVisitor : DocumentVisitor
    {
        public override string VisitPlainText(PlainText text) => "<p>";
        public override string VisitHyperlink(Hyperlink link) => "<a>";
    }
    """;

    [Fact]
    public void Detects_the_double_dispatch_loop()
    {
        var graph = TestGraph.From(DocumentExport);

        var matches = _detector.Detect(graph);

        var match = Assert.Single(matches);
        Assert.Equal("DocumentVisitor", match.Bindings["visitor"]);
        Assert.Equal("HtmlExportVisitor", match.Bindings["concreteVisitor"]);
        Assert.Equal("DocumentPart", match.Bindings["element"]);
        Assert.Equal("Hyperlink", match.Bindings["concreteElementA"]);
        Assert.Equal("PlainText", match.Bindings["concreteElementB"]);
    }

    [Fact]
    public void Ignores_a_state_whose_handle_takes_the_context()
    {
        // A State's abstract handle also takes a foreign class as parameter,
        // but the coupling is one-way: the context declares no abstract
        // operation pointing back at concrete states.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class LightState { public abstract string Handle(LightSwitch lightSwitch); }

        public sealed class OnState : LightState
        {
            public override string Handle(LightSwitch lightSwitch) => "off";
        }

        public sealed class OffState : LightState
        {
            public override string Handle(LightSwitch lightSwitch) => "on";
        }

        public sealed class LightSwitch
        {
            private LightState _state = new OffState();
            public void Apply(LightState state) => _state = state;
            public string Toggle() => _state.Handle(this);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_builder_whose_steps_take_no_elements()
    {
        // A Builder also declares a family of abstract methods, but its steps
        // take primitives, not concrete siblings of a common abstraction.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Car { public int Seats { get; init; } }

        public abstract class CarBuilder
        {
            public abstract void SetSeats(int number);
            public abstract void SetEngine(string engine);
            public abstract Car Build();
        }

        public sealed class SportsCarBuilder : CarBuilder
        {
            private int _seats;
            public override void SetSeats(int number) => _seats = number;
            public override void SetEngine(string engine) { }
            public override Car Build() => new Car { Seats = _seats };
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_callback_over_a_single_element_type()
    {
        // Visiting one concrete type is a plain callback; the pattern's
        // fan-out needs a visit per element of the hierarchy.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class NodeVisitor { public abstract string VisitLeaf(Leaf leaf); }

        public abstract class Node { public abstract string Accept(NodeVisitor visitor); }

        public sealed class Leaf : Node
        {
            public override string Accept(NodeVisitor visitor) => visitor.VisitLeaf(this);
        }

        public sealed class PrintVisitor : NodeVisitor
        {
            public override string VisitLeaf(Leaf leaf) => "leaf";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

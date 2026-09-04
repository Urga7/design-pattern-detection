using DesignPatternDetection.Detection.Patterns.Structural;

namespace DesignPatternDetection.Tests.Detection.Patterns.Structural;

public class CompositePatternDetectorTests
{
    private readonly CompositePatternDetector _detector = new();

    [Fact]
    public void Detects_a_composite_holding_a_collection_of_components()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Graphic { public abstract string Draw(); }

        public sealed class Dot : Graphic
        {
            public override string Draw() => ".";
        }

        public sealed class CompoundGraphic : Graphic
        {
            private readonly List<Graphic> _children = [];
            public void Add(Graphic child) => _children.Add(child);
            public override string Draw() => string.Join(" ", _children.Select(c => c.Draw()));
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("Graphic", match.Bindings["component"]);
        Assert.Equal("CompoundGraphic", match.Bindings["composite"]);
        Assert.Equal("Dot", match.Bindings["leaf"]);

    }

    [Fact]
    public void Detects_children_held_in_an_array()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Shape { public abstract double Area(); }

        public sealed class Circle : Shape
        {
            public override double Area() => 3.14;
        }

        public sealed class ShapeGroup : Shape
        {
            private readonly Shape[] _shapes = [];
            public override double Area() => _shapes.Sum(s => s.Area());
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("ShapeGroup", match.Bindings["composite"]);
    }

    [Fact]
    public void Ignores_a_decorator_wrapping_a_single_component()
    {
        // A Decorator holds one reference to its abstraction; a Composite
        // holds a collection of them.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Graphic { public abstract string Draw(); }

        public sealed class Dot : Graphic
        {
            public override string Draw() => ".";
        }

        public sealed class BorderedGraphic : Graphic
        {
            private readonly Graphic _inner;
            public BorderedGraphic(Graphic inner) => _inner = inner;
            public override string Draw() => $"[{_inner.Draw()}]";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_composite_without_a_leaf_sibling()
    {
        // Without a Leaf there is no part-whole tree - nothing to compose.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Graphic { public abstract string Draw(); }

        public sealed class CompoundGraphic : Graphic
        {
            private readonly List<Graphic> _children = [];
            public override string Draw() => string.Join(" ", _children.Select(c => c.Draw()));
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_collection_of_a_foreign_type()
    {
        // Holding a collection of something other than the own abstraction is
        // plain aggregation, not a Composite.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Graphic { public abstract string Draw(); }

        public sealed class Dot : Graphic
        {
            public override string Draw() => ".";
        }

        public sealed class LabelledGraphic : Graphic
        {
            private readonly List<string> _labels = [];
            public override string Draw() => string.Join(" ", _labels);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

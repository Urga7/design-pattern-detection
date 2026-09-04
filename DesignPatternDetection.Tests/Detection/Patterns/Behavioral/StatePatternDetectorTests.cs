using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class StatePatternDetectorTests
{
    private readonly StatePatternDetector _detector = new();

    private const string ToggleSwitch = """
    namespace Demo;

    public abstract class LightState { public abstract string Handle(LightSwitch lightSwitch); }

    public sealed class OnState : LightState
    {
        public override string Handle(LightSwitch lightSwitch)
        {
            lightSwitch.Apply(new OffState());
            return "off";
        }
    }

    public sealed class OffState : LightState
    {
        public override string Handle(LightSwitch lightSwitch)
        {
            lightSwitch.Apply(new OnState());
            return "on";
        }
    }

    public sealed class LightSwitch
    {
        private LightState _state = new OffState();
        public void Apply(LightState state) => _state = state;
        public string Toggle() => _state.Handle(this);
    }
    """;

    [Fact]
    public void Detects_one_match_per_transitioning_state()
    {
        var graph = TestGraph.From(ToggleSwitch);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal("LightSwitch", match.Bindings["context"]);
            Assert.Equal("LightState", match.Bindings["state"]);
            Assert.NotEqual(match.Bindings["concreteState"], match.Bindings["nextState"]);
        });
        Assert.Equal(["OffState", "OnState"],
            matches.Select(m => m.Bindings["concreteState"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_strategy_whose_concrete_classes_never_transition()
    {
        // Interchangeable algorithms know nothing about one another; without
        // a sibling instantiation there is no machine moving between states.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class SortStrategy { public abstract string Sort(string items); }
        public sealed class BubbleSort : SortStrategy { public override string Sort(string items) => "bubble"; }
        public sealed class QuickSort : SortStrategy { public override string Sort(string items) => "quick"; }

        public sealed class Sorter
        {
            private SortStrategy _strategy;
            public Sorter(SortStrategy strategy) => _strategy = strategy;
            public string Sort(string items) => _strategy.Sort(items);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_lazy_proxy_that_caches_the_sibling_it_creates()
    {
        // A proxy also instantiates a sibling from its own hierarchy, but it
        // keeps the created instance in a field; a state hands control over.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Image { public abstract string Display(); }
        public sealed class RealImage : Image { public override string Display() => "bytes"; }

        public sealed class ImageProxy : Image
        {
            private RealImage _real;

            public override string Display()
            {
                _real = new RealImage();
                return _real.Display();
            }
        }

        public sealed class Gallery
        {
            private readonly Image _image;
            public Gallery(Image image) => _image = image;
            public string Show() => _image.Display();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_transitions_without_a_context_wrapping_the_abstraction()
    {
        // Sibling-creating subclasses alone are not the pattern: without a
        // context delegating to the abstraction there is no machine.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class LightState { public abstract string Handle(); }

        public sealed class OnState : LightState
        {
            public override string Handle() => new OffState().ToString();
        }

        public sealed class OffState : LightState
        {
            public override string Handle() => new OnState().ToString();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

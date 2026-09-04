using DesignPatternDetection.Detection.Patterns.Structural;

namespace DesignPatternDetection.Tests.Detection.Patterns.Structural;

public class BridgePatternDetectorTests
{
    private readonly BridgePatternDetector _detector = new();

    private const string RemoteControl = """
    namespace Demo;

    public abstract class Device { public abstract string PowerOn(); }
    public sealed class Tv : Device { public override string PowerOn() => "tv"; }
    public sealed class Radio : Device { public override string PowerOn() => "radio"; }

    public class Remote
    {
        protected readonly Device Device;
        public Remote(Device device) => Device = device;
        public string TurnOn() => Device.PowerOn();
    }

    public sealed class AdvancedRemote : Remote
    {
        public AdvancedRemote(Device device) : base(device) { }
        public string TurnOnMuted() => Device.PowerOn() + " (muted)";
    }
    """;

    [Fact]
    public void Detects_one_match_per_concrete_implementor()
    {
        var graph = TestGraph.From(RemoteControl);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal("Remote", match.Bindings["abstraction"]);
            Assert.Equal("AdvancedRemote", match.Bindings["refinedAbstraction"]);
            Assert.Equal("Device", match.Bindings["implementor"]);
        });
        Assert.Equal(["Radio", "Tv"],
            matches.Select(m => m.Bindings["concreteImplementor"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_lone_wrapper_with_no_refined_abstraction()
    {
        // Without a subclass on the wrapping side only the implementors vary -
        // that is Strategy-like composition, not a bridge between hierarchies.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Device { public abstract string PowerOn(); }
        public sealed class Tv : Device { public override string PowerOn() => "tv"; }

        public sealed class Remote
        {
            private readonly Device _device;
            public Remote(Device device) => _device = device;
            public string TurnOn() => _device.PowerOn();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_wrapper_around_a_class_without_its_own_hierarchy()
    {
        // Wrapping a concrete class that heads no hierarchy is Adapter-shaped
        // composition: nothing on the implementation side can vary.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class LegacyPager { public string Page() => "page"; }

        public class Remote
        {
            protected readonly LegacyPager Pager;
            public Remote(LegacyPager pager) => Pager = pager;
            public string TurnOn() => Pager.Page();
        }

        public sealed class AdvancedRemote : Remote
        {
            public AdvancedRemote(LegacyPager pager) : base(pager) { }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_decorator_whose_wrapper_extends_the_wrapped_type()
    {
        // A Decorator's wrapper belongs to the hierarchy it wraps; a Bridge
        // joins two unrelated hierarchies.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(); }
        public sealed class EmailNotifier : Notifier { public override string Send() => "email"; }

        public class NotifierDecorator : Notifier
        {
            protected readonly Notifier Inner;
            public NotifierDecorator(Notifier inner) => Inner = inner;
            public override string Send() => Inner.Send();
        }

        public sealed class LoggingNotifier : NotifierDecorator
        {
            public LoggingNotifier(Notifier inner) : base(inner) { }
            public override string Send() => "log: " + Inner.Send();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class StrategyPatternDetectorTests
{
    private readonly StrategyPatternDetector _detector = new();

    private const string Sorting = """
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
    """;

    [Fact]
    public void Detects_one_match_per_concrete_strategy()
    {
        var graph = TestGraph.From(Sorting);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal("Sorter", match.Bindings["context"]);
            Assert.Equal("SortStrategy", match.Bindings["strategy"]);
        });
        Assert.Equal(["BubbleSort", "QuickSort"],
            matches.Select(m => m.Bindings["concreteStrategy"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_bridge_whose_wrapper_heads_its_own_hierarchy()
    {
        // Once the wrapping side has a refined subclass of its own, both
        // sides vary independently - that is a Bridge, not a swappable
        // algorithm behind a lone context.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Device { public abstract string PowerOn(); }
        public sealed class Tv : Device { public override string PowerOn() => "tv"; }

        public class Remote
        {
            protected readonly Device Device;
            public Remote(Device device) => Device = device;
            public string TurnOn() => Device.PowerOn();
        }

        public sealed class AdvancedRemote : Remote
        {
            public AdvancedRemote(Device device) : base(device) { }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_decorator_that_extends_the_abstraction_it_wraps()
    {
        // A Decorator also holds a field typed as the abstraction, but its
        // wrapper belongs to that very hierarchy; a Strategy context does not.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class DataSource { public abstract string Read(); }
        public sealed class FileDataSource : DataSource { public override string Read() => "raw"; }

        public sealed class EncryptionDecorator : DataSource
        {
            private readonly DataSource _inner;
            public EncryptionDecorator(DataSource inner) => _inner = inner;
            public override string Read() => _inner.Read();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_command_whose_concrete_classes_wrap_a_receiver()
    {
        // Concrete commands delegate the work to a wrapped Receiver class;
        // concrete strategies are self-contained algorithms.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Command { public abstract string Execute(); }

        public sealed class Light { public string TurnOn() => "on"; }

        public sealed class LightOnCommand : Command
        {
            private readonly Light _light;
            public LightOnCommand(Light light) => _light = light;
            public override string Execute() => _light.TurnOn();
        }

        public sealed class RemoteButton
        {
            private readonly Command _command;
            public RemoteButton(Command command) => _command = command;
            public string Press() => _command.Execute();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_state_machine_whose_concrete_classes_instantiate_siblings()
    {
        // Concrete states drive the machine onward by creating the sibling
        // that takes over next; interchangeable strategies never reference
        // one another.
        var graph = TestGraph.From("""
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
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_command_hierarchy_with_a_receiverless_member()
    {
        // A command hierarchy may mix receiver-wrapping commands with simple
        // self-contained ones; the presence of any receiver-wrapping sibling
        // marks the whole hierarchy as reified requests, not algorithms.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface ICommand { void Execute(); }

        public sealed class SimpleCommand : ICommand
        {
            public void Execute() { }
        }

        public sealed class Receiver { public void DoSomething() { } }

        public sealed class ComplexCommand : ICommand
        {
            private Receiver _receiver;
            public void Execute() => _receiver.DoSomething();
        }

        public sealed class Invoker
        {
            private ICommand _onStart;
            public void Start() => _onStart.Execute();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

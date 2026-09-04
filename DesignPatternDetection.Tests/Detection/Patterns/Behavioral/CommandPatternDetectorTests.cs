using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class CommandPatternDetectorTests
{
    private readonly CommandPatternDetector _detector = new();

    private const string LightSwitch = """
    namespace Demo;

    public abstract class Command { public abstract string Execute(); }

    public sealed class Light
    {
        public string TurnOn() => "on";
        public string TurnOff() => "off";
    }

    public sealed class LightOnCommand : Command
    {
        private readonly Light _light;
        public LightOnCommand(Light light) => _light = light;
        public override string Execute() => _light.TurnOn();
    }

    public sealed class LightOffCommand : Command
    {
        private readonly Light _light;
        public LightOffCommand(Light light) => _light = light;
        public override string Execute() => _light.TurnOff();
    }

    public sealed class RemoteButton
    {
        private readonly Command _command;
        public RemoteButton(Command command) => _command = command;
        public string Press() => _command.Execute();
    }
    """;

    [Fact]
    public void Detects_one_match_per_concrete_command()
    {
        var graph = TestGraph.From(LightSwitch);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal("RemoteButton", match.Bindings["invoker"]);
            Assert.Equal("Command", match.Bindings["command"]);
            Assert.Equal("Light", match.Bindings["receiver"]);
        });
        Assert.Equal(["LightOffCommand", "LightOnCommand"],
            matches.Select(m => m.Bindings["concreteCommand"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_strategy_whose_concrete_strategies_wrap_no_receiver()
    {
        // Without a Receiver there is nothing the command defers work to -
        // a context delegating to self-contained algorithms is a Strategy.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class SortStrategy { public abstract string Sort(string items); }
        public sealed class BubbleSort : SortStrategy { public override string Sort(string items) => "bubble"; }

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
    public void Ignores_an_adapter_when_nothing_stores_the_target_abstraction()
    {
        // An Adapter wraps a foreign class exactly like a ConcreteCommand,
        // but no invoker keeps the Target in a field for later invocation.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(string message); }

        public sealed class LegacyPager { public string Page(string text) => text; }

        public sealed class PagerAdapter : Notifier
        {
            private readonly LegacyPager _pager;
            public PagerAdapter(LegacyPager pager) => _pager = pager;
            public override string Send(string message) => _pager.Page(message);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_proxy_whose_wrapped_class_belongs_to_the_hierarchy()
    {
        // A Proxy wraps a concrete sibling from its own hierarchy; a
        // ConcreteCommand's receiver stands outside the command hierarchy.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Image { public abstract string Display(); }
        public sealed class RealImage : Image { public override string Display() => "bytes"; }

        public sealed class ImageProxy : Image
        {
            private readonly RealImage _real;
            public ImageProxy(RealImage real) => _real = real;
            public override string Display() => _real.Display();
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
    public void Ignores_a_mediator_whose_wrapped_classes_talk_back()
    {
        // A ConcreteMediator also wraps foreign classes while an outside
        // class stores the abstraction, but the coupling is bidirectional:
        // each colleague holds the mediator abstraction, whereas a Receiver
        // knows nothing about the command that drives it.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class DialogMediator { public abstract string WidgetChanged(string widget); }

        public sealed class TextBox
        {
            private readonly DialogMediator _dialog;
            public TextBox(DialogMediator dialog) => _dialog = dialog;
            public string Type(string text) => _dialog.WidgetChanged(text);
        }

        public sealed class SubmitButton
        {
            private readonly DialogMediator _dialog;
            public SubmitButton(DialogMediator dialog) => _dialog = dialog;
            public string Click() => _dialog.WidgetChanged("click");
        }

        public sealed class LoginDialog : DialogMediator
        {
            private TextBox? _username;
            private SubmitButton? _submit;
            public void Register(TextBox username, SubmitButton submit) { _username = username; _submit = submit; }
            public override string WidgetChanged(string widget) => widget;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_builder_that_manufactures_what_it_forwards_to()
    {
        // A ConcreteBuilder is command-shaped - it implements an abstraction,
        // forwards its steps to a wrapped Product, and a Director stores the
        // abstraction to trigger it - but a command's receiver is handed in
        // from outside, never manufactured by the wrapper itself.
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

        public sealed class Director
        {
            private IBuilder _builder;
            public void Construct() => _builder.BuildPartA();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class MediatorPatternDetectorTests
{
    private readonly MediatorPatternDetector _detector = new();

    private const string LoginDialog = """
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
    """;

    [Fact]
    public void Detects_the_mediator_and_both_colleagues()
    {
        var graph = TestGraph.From(LoginDialog);

        var matches = _detector.Detect(graph);

        var match = Assert.Single(matches);
        Assert.Equal("DialogMediator", match.Bindings["mediator"]);
        Assert.Equal("LoginDialog", match.Bindings["concreteMediator"]);
        Assert.Equal("SubmitButton", match.Bindings["colleagueA"]);
        Assert.Equal("TextBox", match.Bindings["colleagueB"]);
    }

    [Fact]
    public void Ignores_a_command_whose_receiver_never_talks_back()
    {
        // A ConcreteCommand also wraps a foreign class, but the coupling is
        // one-way: the Receiver holds no reference to the command abstraction.
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
    public void Ignores_a_mutual_pair_with_a_single_peer()
    {
        // One class pointing back at the hub is a plain mutual association
        // (Iterator-shaped); a mediator fans out over several colleagues.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class ChatMediator { public abstract string Broadcast(string message); }

        public sealed class Participant
        {
            private readonly ChatMediator _chat;
            public Participant(ChatMediator chat) => _chat = chat;
            public string Send(string message) => _chat.Broadcast(message);
        }

        public sealed class DirectChat : ChatMediator
        {
            private Participant? _only;
            public void Register(Participant only) => _only = only;
            public override string Broadcast(string message) => message;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_widget_tree_whose_children_belong_to_the_hierarchy()
    {
        // Children referencing a parent of their own kind form a tree
        // (Composite-shaped); mediator colleagues stand outside the
        // mediator's hierarchy.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Widget { public abstract string Render(); }

        public sealed class Button : Widget
        {
            private readonly Widget _parent;
            public Button(Widget parent) => _parent = parent;
            public override string Render() => "button";
        }

        public sealed class Label : Widget
        {
            private readonly Widget _parent;
            public Label(Widget parent) => _parent = parent;
            public override string Render() => "label";
        }

        public sealed class Panel : Widget
        {
            private Button? _button;
            private Label? _label;
            public void Add(Button button, Label label) { _button = button; _label = label; }
            public override string Render() => "panel";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Detects_colleagues_reporting_through_an_inherited_mediator_field()
    {
        // Idiomatic C# keeps the mediator reference on a colleague base
        // class; the concrete colleagues report through the inherited field.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface IMediator { void Notify(string ev); }

        public class BaseComponent
        {
            protected IMediator _mediator;
            public void SetMediator(IMediator mediator) => _mediator = mediator;
        }

        public sealed class Component1 : BaseComponent
        {
            public void DoA() => _mediator.Notify("A");
        }

        public sealed class Component2 : BaseComponent
        {
            public void DoC() => _mediator.Notify("C");
        }

        public sealed class ConcreteMediator : IMediator
        {
            private Component1 _component1;
            private Component2 _component2;
            public void Notify(string ev) => _component1.DoA();
        }
        """);

        var matches = _detector.Detect(graph);

        Assert.NotEmpty(matches);
        Assert.All(matches, match =>
        {
            Assert.Equal("IMediator", match.Bindings["mediator"]);
            Assert.Equal("ConcreteMediator", match.Bindings["concreteMediator"]);
        });
    }
}

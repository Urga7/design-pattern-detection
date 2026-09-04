using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// Mediator: the abstraction through which the widgets talk to one another.
[UsedImplicitly]
public abstract class DialogMediator
{
    public abstract string WidgetChanged(string widget);
}

// Colleagues: each widget reports to the mediator instead of to its peers.
[UsedImplicitly]
public sealed class TextBox
{
    private readonly DialogMediator _dialog;

    public TextBox(DialogMediator dialog) => _dialog = dialog;

    public string Type(string text) => _dialog.WidgetChanged($"textbox typed {text}");
}

[UsedImplicitly]
public sealed class SubmitButton
{
    private readonly DialogMediator _dialog;

    public SubmitButton(DialogMediator dialog) => _dialog = dialog;

    public string Click() => _dialog.WidgetChanged("button clicked");
}

// ConcreteMediator: knows every widget and coordinates their interaction.
[UsedImplicitly]
public sealed class LoginDialog : DialogMediator
{
    private TextBox? _username;
    private SubmitButton? _submit;

    public void Register(TextBox username, SubmitButton submit)
    {
        _username = username;
        _submit = submit;
    }

    public override string WidgetChanged(string widget) =>
        $"login dialog reacts to: {widget} (username {_username is null}, submit {_submit is null})";
}

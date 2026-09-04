using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// Receiver: the class that knows how to perform the actual work.
[UsedImplicitly]
public sealed class Light
{
    public string TurnOn() => "Light is on.";

    public string TurnOff() => "Light is off.";
}

// Command: reifies a request as an object with an abstract Execute.
[UsedImplicitly]
public abstract class Command
{
    public abstract string Execute();
}

// Concrete commands: each wraps the receiver and binds it to one action.
[UsedImplicitly]
public sealed class LightOnCommand : Command
{
    private readonly Light _light;

    public LightOnCommand(Light light) => _light = light;

    public override string Execute() => _light.TurnOn();
}

[UsedImplicitly]
public sealed class LightOffCommand : Command
{
    private readonly Light _light;

    public LightOffCommand(Light light) => _light = light;

    public override string Execute() => _light.TurnOff();
}

// Invoker: stores the command through its abstraction and triggers it later, knowing nothing about the receiver behind it.
[UsedImplicitly]
public sealed class RemoteButton
{
    private readonly Command _command;

    public RemoteButton(Command command) => _command = command;

    public string Press() => _command.Execute();
}

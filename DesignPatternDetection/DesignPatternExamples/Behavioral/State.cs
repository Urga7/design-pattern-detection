using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// State: the abstract behavior that changes with the machine's condition.
[UsedImplicitly]
public abstract class LightState
{
    public abstract string Handle(LightSwitch lightSwitch);
}

// Concrete states: each handles the request and transitions the machine by creating the sibling state that takes over next.
[UsedImplicitly]
public sealed class OnState : LightState
{
    public override string Handle(LightSwitch lightSwitch)
    {
        lightSwitch.Apply(new OffState());
        return "switched off";
    }
}

[UsedImplicitly]
public sealed class OffState : LightState
{
    public override string Handle(LightSwitch lightSwitch)
    {
        lightSwitch.Apply(new OnState());
        return "switched on";
    }
}

// Context: wraps the current state and delegates to it; the states themselves decide what comes next.
[UsedImplicitly]
public sealed class LightSwitch
{
    private LightState _state = new OffState();

    public void Apply(LightState state) => _state = state;

    public string Toggle() => _state.Handle(this);
}

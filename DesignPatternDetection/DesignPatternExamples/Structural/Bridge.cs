using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Structural;

// Implementor: the platform side of the bridge, heading its own hierarchy.
[UsedImplicitly]
public abstract class Device
{
    public abstract string PowerOn();
}

// Concrete implementors.
[UsedImplicitly]
public sealed class Tv : Device
{
    public override string PowerOn() => "TV is on.";
}

[UsedImplicitly]
public sealed class Radio : Device
{
    public override string PowerOn() => "Radio is on.";
}

// Abstraction: holds a reference to an implementor and delegates to it.
[UsedImplicitly]
public class Remote
{
    protected readonly Device Device;

    public Remote(Device device) => Device = device;

    public string TurnOn() => Device.PowerOn();
}

// Refined abstraction: extends the control side independently of the devices.
[UsedImplicitly]
public sealed class AdvancedRemote : Remote
{
    public AdvancedRemote(Device device) : base(device) { }

    public string TurnOnMuted() => $"{Device.PowerOn()} (muted)";
}

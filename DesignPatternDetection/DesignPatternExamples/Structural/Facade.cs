using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Structural;

// Subsystem classes: each does its own low-level piece of work.
[UsedImplicitly]
public sealed class Cpu
{
    public string Execute() => "executing";
}

[UsedImplicitly]
public sealed class Memory
{
    public string Load() => "loading";
}

// Facade: fronts the subsystem with one simple entry point, conforming to no abstraction of its own.
[UsedImplicitly]
public sealed class ComputerFacade
{
    private readonly Cpu _cpu = new();
    private readonly Memory _memory = new();

    public string Start() => $"{_memory.Load()}, {_cpu.Execute()}";
}

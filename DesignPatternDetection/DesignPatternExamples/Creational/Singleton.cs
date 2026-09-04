using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Creational;

[UsedImplicitly]
public sealed class Singleton
{
    private Singleton() {}
    
    public static Singleton Instance { get; } = new Singleton();
}
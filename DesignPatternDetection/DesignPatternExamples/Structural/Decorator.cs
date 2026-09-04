using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Structural;

// Component: the abstraction clients read from.
[UsedImplicitly]
public abstract class DataSource
{
    public abstract string Read();
}

// Concrete component: the plain object that gets decorated.
[UsedImplicitly]
public sealed class FileDataSource : DataSource
{
    public override string Read() => "raw data";
}

// Decorator: wraps the very abstraction it implements and adds behavior around the delegated call.
[UsedImplicitly]
public sealed class EncryptionDecorator : DataSource
{
    private readonly DataSource _inner;

    public EncryptionDecorator(DataSource inner) => _inner = inner;

    public override string Read() => $"decrypted({_inner.Read()})";
}

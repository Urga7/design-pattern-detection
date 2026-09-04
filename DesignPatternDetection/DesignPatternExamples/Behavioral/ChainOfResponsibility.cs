using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// Handler: declares the request operation and the successor link that lines the handlers up into a chain.
[UsedImplicitly]
public abstract class SupportHandler
{
    protected readonly SupportHandler? Next;

    protected SupportHandler(SupportHandler? next) => Next = next;

    public abstract string Handle(string request);
}

// Concrete handlers: each resolves the requests it is responsible for and passes everything else on to its successor.
[UsedImplicitly]
public sealed class FirstLevelSupport : SupportHandler
{
    public FirstLevelSupport(SupportHandler? next) : base(next) { }

    public override string Handle(string request) =>
        request == "password reset"
            ? "first level resolved it"
            : Next?.Handle(request) ?? "nobody could help";
}

[UsedImplicitly]
public sealed class SecondLevelSupport : SupportHandler
{
    public SecondLevelSupport(SupportHandler? next) : base(next) { }

    public override string Handle(string request) =>
        request == "server down"
            ? "second level resolved it"
            : Next?.Handle(request) ?? "nobody could help";
}

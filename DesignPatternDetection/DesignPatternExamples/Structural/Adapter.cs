using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Structural;

// Target: the abstraction the client code works with.
[UsedImplicitly]
public abstract class Notifier
{
    public abstract string Send(string message);
}

// Adaptee: an existing class with an incompatible interface.
[UsedImplicitly]
public sealed class LegacyPager
{
    public string Page(string text) => $"PAGE: {text}";
}

// Adapter: implements the Target by delegating to the wrapped Adaptee.
[UsedImplicitly]
public sealed class PagerAdapter : Notifier
{
    private readonly LegacyPager _pager;

    public PagerAdapter(LegacyPager pager) => _pager = pager;

    public override string Send(string message) => _pager.Page(message);
}

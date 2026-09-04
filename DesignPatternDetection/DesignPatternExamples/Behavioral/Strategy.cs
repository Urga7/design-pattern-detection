using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// Strategy: the abstract algorithm the context delegates to.
[UsedImplicitly]
public abstract class SortStrategy
{
    public abstract string Sort(string items);
}

// Concrete strategies: interchangeable, self-contained algorithms.
[UsedImplicitly]
public sealed class BubbleSort : SortStrategy
{
    public override string Sort(string items) => $"bubble({items})";
}

[UsedImplicitly]
public sealed class QuickSort : SortStrategy
{
    public override string Sort(string items) => $"quick({items})";
}

// Context: wraps a strategy without belonging to its hierarchy, so the algorithm can be swapped at runtime.
[UsedImplicitly]
public sealed class Sorter
{
    private SortStrategy _strategy;

    public Sorter(SortStrategy strategy) => _strategy = strategy;

    public void ChangeStrategy(SortStrategy strategy) => _strategy = strategy;

    public string Sort(string items) => _strategy.Sort(items);
}

using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Structural;

// Component: the operation shared by leaves and composites alike.
[UsedImplicitly]
public abstract class Graphic
{
    public abstract string Draw();
}

// Leaf: implements the operation with no children of its own.
[UsedImplicitly]
public sealed class Dot : Graphic
{
    public override string Draw() => ".";
}

// Composite: holds child Components and delegates the operation to them.
[UsedImplicitly]
public sealed class CompoundGraphic : Graphic
{
    private readonly List<Graphic> _children = [];

    public void Add(Graphic child) => _children.Add(child);

    public override string Draw() => string.Join(" ", _children.Select(child => child.Draw()));
}

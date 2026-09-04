using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Structural;

// Flyweight: immutable intrinsic state shared by many trees; the extrinsic state (coordinates) is passed in from outside.
[UsedImplicitly]
public sealed class TreeType
{
    private readonly string _name;
    private readonly string _texture;

    public TreeType(string name, string texture)
    {
        _name = name;
        _texture = texture;
    }

    public string Draw(int x, int y) => $"{_name}[{_texture}] at ({x}, {y})";
}

// FlyweightFactory: pools the flyweights and hands out shared instances, creating one only when the pool has no match.
[UsedImplicitly]
public sealed class TreeFactory
{
    private readonly Dictionary<string, TreeType> _pool = new();

    public TreeType GetTreeType(string name, string texture)
    {
        if (!_pool.TryGetValue(name, out var type))
        {
            type = new TreeType(name, texture);
            _pool[name] = type;
        }

        return type;
    }
}

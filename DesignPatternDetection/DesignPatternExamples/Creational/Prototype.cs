using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Creational;

// Prototype: declares the clone operation that returns a copy of its own type.
[UsedImplicitly]
public abstract class Shape
{
    public int X { get; set; }
    public int Y { get; set; }

    protected Shape() { }
    protected Shape(Shape source)
    {
        X = source.X;
        Y = source.Y;
    }

    public abstract Shape Clone();
}

// Concrete prototypes: each clones itself through a copy constructor.
[UsedImplicitly]
public sealed class Circle : Shape
{
    public int Radius { get; set; }

    public Circle() { }
    private Circle(Circle source) : base(source) => Radius = source.Radius;

    public override Shape Clone() => new Circle(this);
}

[UsedImplicitly]
public sealed class Rectangle : Shape
{
    public int Width { get; set; }
    public int Height { get; set; }

    public Rectangle() { }
    private Rectangle(Rectangle source) : base(source)
    {
        Width = source.Width;
        Height = source.Height;
    }

    public override Shape Clone() => new Rectangle(this);
}

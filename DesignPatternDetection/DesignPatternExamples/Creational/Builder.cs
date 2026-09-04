using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Creational;

// Product: the complex object assembled piece by piece.
[UsedImplicitly]
public sealed class Car
{
    public int Seats { get; init; }
    public string Engine { get; init; } = "";
    public bool HasGps { get; init; }
}

// Builder: one abstract step per part of the product, plus an assembling method that hands back the finished result.
[UsedImplicitly]
public abstract class CarBuilder
{
    public abstract void SetSeats(int number);
    public abstract void SetEngine(string engine);
    public abstract void SetGps(bool installed);
    public abstract Car Build();
}

// Concrete builders: each overrides the steps and assembles a Car it builds itself.
[UsedImplicitly]
public sealed class SportsCarBuilder : CarBuilder
{
    private int _seats;
    private string _engine = "";
    private bool _gps;

    public override void SetSeats(int number) => _seats = number;
    public override void SetEngine(string engine) => _engine = engine;
    public override void SetGps(bool installed) => _gps = installed;

    public override Car Build() => new Car { Seats = _seats, Engine = _engine, HasGps = _gps };
}

[UsedImplicitly]
public sealed class SuvBuilder : CarBuilder
{
    private int _seats;
    private string _engine = "";
    private bool _gps;

    public override void SetSeats(int number) => _seats = number;
    public override void SetEngine(string engine) => _engine = engine;
    public override void SetGps(bool installed) => _gps = installed;

    public override Car Build() => new Car { Seats = _seats, Engine = _engine, HasGps = _gps };
}

// Director: drives a builder through the steps in a fixed order, without knowing which concrete Car comes out the other end.
[UsedImplicitly]
public sealed class CarDirector
{
    public Car Construct(CarBuilder builder)
    {
        builder.SetSeats(2);
        builder.SetEngine("V8");
        builder.SetGps(true);
        return builder.Build();
    }
}

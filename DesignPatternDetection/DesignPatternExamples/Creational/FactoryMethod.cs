using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Creational;

// Product: the abstract type the factory method returns.
[UsedImplicitly]
public abstract class Transport
{
    public abstract string Deliver();
}

// Concrete products.
[UsedImplicitly]
public sealed class Truck : Transport
{
    public override string Deliver() => "Delivering by land in a box.";
}

[UsedImplicitly]
public sealed class Ship : Transport
{
    public override string Deliver() => "Delivering by sea in a container.";
}

// Creator: declares the factory method (CreateTransport) and uses it.
[UsedImplicitly]
public abstract class Logistics
{
    public abstract Transport CreateTransport();

    public string PlanDelivery() => CreateTransport().Deliver();
}

// Concrete creators: each overrides the factory method to build a concrete product.
[UsedImplicitly]
public sealed class RoadLogistics : Logistics
{
    public override Transport CreateTransport() => new Truck();
}

[UsedImplicitly]
public sealed class SeaLogistics : Logistics
{
    public override Transport CreateTransport() => new Ship();
}

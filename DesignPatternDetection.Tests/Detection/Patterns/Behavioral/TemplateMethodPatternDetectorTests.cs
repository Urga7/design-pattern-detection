using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class TemplateMethodPatternDetectorTests
{
    private readonly TemplateMethodPatternDetector _detector = new();

    private const string Exporters = """
    namespace Demo;

    public abstract class DataExporter
    {
        public string Export() => ReadData() + FormatData();
        protected abstract string ReadData();
        protected abstract string FormatData();
    }

    public sealed class CsvExporter : DataExporter
    {
        protected override string ReadData() => "rows";
        protected override string FormatData() => "csv";
    }

    public sealed class JsonExporter : DataExporter
    {
        protected override string ReadData() => "documents";
        protected override string FormatData() => "json";
    }
    """;

    [Fact]
    public void Detects_one_match_per_concrete_class()
    {
        var graph = TestGraph.From(Exporters);

        var matches = _detector.Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => Assert.Equal("DataExporter", match.Bindings["abstractClass"]));
        Assert.Equal(["CsvExporter", "JsonExporter"],
            matches.Select(m => m.Bindings["concreteClass"]).OrderBy(name => name));
    }

    [Fact]
    public void Ignores_a_factory_method_creator()
    {
        // A Creator has the same split - a concrete method driving an
        // abstract step - but its step's override manufactures a subtype of
        // the step's return type, which is Factory Method's defining trait.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Transport { public abstract string Deliver(); }
        public sealed class Truck : Transport { public override string Deliver() => "land"; }

        public abstract class Logistics
        {
            public abstract Transport CreateTransport();
            public string PlanDelivery() => CreateTransport().Deliver();
        }

        public sealed class RoadLogistics : Logistics
        {
            public override Transport CreateTransport() => new Truck();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_pure_abstract_class_with_no_template_to_inherit()
    {
        // All-abstract classes (Builder, Abstract Factory) defer everything:
        // without a concrete method there is no fixed skeleton.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class CarBuilder
        {
            public abstract void SetSeats(int number);
            public abstract void SetEngine(string engine);
        }

        public sealed class SportsCarBuilder : CarBuilder
        {
            public override void SetSeats(int number) { }
            public override void SetEngine(string engine) { }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_static_helper_beside_the_abstract_steps()
    {
        // A static method belongs to the class, not the algorithm skeleton
        // its instances inherit - it cannot call the abstract steps.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class DataExporter
        {
            public static string Describe() => "exporter";
            protected abstract string ReadData();
        }

        public sealed class CsvExporter : DataExporter
        {
            protected override string ReadData() => "rows";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_template_that_never_drives_its_abstract_step()
    {
        // A concrete method sitting beside an abstract one is not a template
        // unless it actually calls the step it supposedly defers.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Repository
        {
            public string Describe() => "repository";
            protected abstract string Load();
        }

        public sealed class FileRepository : Repository
        {
            protected override string Load() => "file";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

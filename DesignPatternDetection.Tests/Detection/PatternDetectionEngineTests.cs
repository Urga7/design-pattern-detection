using DesignPatternDetection.Detection;

namespace DesignPatternDetection.Tests.Detection;

public class PatternDetectionEngineTests
{
    [Fact]
    public void Default_engine_discovers_every_detector_and_reports_matches()
    {
        using var directory = new TempDirectory("dpd-engine-tests-");
        directory.Write("Source.cs", Singleton);

        var writer = new StringWriter();
        var engine = new PatternDetectionEngine();
        engine.Report(engine.Scan(Directory.GetFiles(directory.Root, "*.cs")), writer);
        var output = writer.ToString();

        Assert.Contains("Abstract Factory", output);
        Assert.Contains("Factory Method", output);
        Assert.Contains("class = Singleton", output);
        Assert.Contains("No instances of the Abstract Factory pattern were found.", output);
    }

    [Fact]
    public void Scan_joins_matches_to_source_locations()
    {
        using var directory = new TempDirectory("dpd-engine-tests-");
        directory.Write("Source.cs", Singleton);

        var result = new PatternDetectionEngine().Scan(Directory.GetFiles(directory.Root, "*.cs"));

        Assert.Equal(1, result.FileCount);
        var singleton = result.Detections.Single(detection => detection.PatternName == "Singleton");
        var match = Assert.Single(singleton.Matches);

        var span = result.Locations[match.Fragments!["class"]];
        Assert.EndsWith("Source.cs", span.FilePath);
        Assert.Equal(2, span.StartLine);
    }

    private const string Singleton = """
        namespace Demo;
        public sealed class Singleton
        {
            private Singleton() { }
            public static Singleton Instance { get; } = new Singleton();
        }
        """;
}

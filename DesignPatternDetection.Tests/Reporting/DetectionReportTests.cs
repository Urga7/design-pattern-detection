using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.Verification;
using DesignPatternDetection.Reporting;
using VDS.RDF;

namespace DesignPatternDetection.Tests.Reporting;

public class DetectionReportTests
{
    private static ScanResult SampleScan() => new(
        2,
        [
            new PatternDetection("Singleton",
            [
                new PatternMatch(
                    "Singleton",
                    new Dictionary<string, string>
                    {
                        ["class"] = "Singleton",
                        ["collection"] = "List_string_"
                    },
                    new Dictionary<string, string>
                    {
                        ["class"] = "Demo.Singleton",
                        ["collection"] = "System.Collections.Generic.List_string_"
                    })
            ]),
            new PatternDetection("Adapter", [])
        ],
        new SourceGraph(
            new Graph(),
            new Dictionary<string, SourceSpan>
            {
                ["Demo.Singleton"] = new(@"C:\src\Singleton.cs", 3, 3)
            }));

    [Fact]
    public void From_resolves_spanned_roles_and_leaves_metadata_roles_bare()
    {
        var report = DetectionReport.From(SampleScan());

        Assert.Equal(2, report.FileCount);
        var match = Assert.Single(report.Patterns.Single(pattern => pattern.Pattern == "Singleton").Matches);

        var spanned = match.Roles.Single(role => role.Role == "class");
        Assert.Equal("Singleton", spanned.Label);
        Assert.Equal(@"C:\src\Singleton.cs", spanned.File);
        Assert.Equal(3, spanned.StartLine);
        Assert.Equal(3, spanned.EndLine);

        var bare = match.Roles.Single(role => role.Role == "collection");
        Assert.Equal("List_string_", bare.Label);
        Assert.Null(bare.File);
        Assert.Null(bare.StartLine);
        Assert.Null(bare.EndLine);
    }

    [Fact]
    public void Save_then_Load_round_trips()
    {
        var report = DetectionReport.From(SampleScan());
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            report.Save(path);
            var loaded = DetectionReport.Load(path);

            Assert.Equal(report.Tool, loaded.Tool);
            Assert.Equal(report.FileCount, loaded.FileCount);
            var match = Assert.Single(loaded.Patterns.Single(pattern => pattern.Pattern == "Singleton").Matches);
            Assert.Equal(@"C:\src\Singleton.cs", match.Roles.Single(role => role.Role == "class").File);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_reviewed_match_reports_the_ruling()
    {
        var scan = SampleScan();
        var reviewed = scan with
        {
            Detections = scan.Detections
                .Select(detection => new PatternDetection(
                    detection.PatternName,
                    detection.Matches
                        .Select(match => match with
                        {
                            Verdict = new MatchVerdict(
                                VerificationOutcome.Rejected,
                                "The instance field holds another type.",
                                "test-model")
                        })
                        .ToList()))
                .ToList()
        };

        var match = DetectionReport.From(reviewed).Patterns.SelectMany(pattern => pattern.Matches).First();

        Assert.Equal("rejected", match.Verdict!.Outcome);
        Assert.Equal("The instance field holds another type.", match.Verdict.Rationale);
        Assert.Equal("test-model", match.Verdict.Model);
    }
}

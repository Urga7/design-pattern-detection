using System.Text.Json;
using DesignPatternDetection.Reporting;

namespace DesignPatternDetection.Tests.Reporting;

public class SarifReportWriterTests
{
    private static DetectionReport SampleReport() => new(
        "DesignPatternDetection",
        "1.0.0",
        DateTimeOffset.UtcNow,
        1,
        [
            new PatternReport("Factory Method",
            [
                new MatchReport(
                    [
                        new RoleBinding("creator", "Creator", @"C:\src\Creator.cs", 3, 10),
                        new RoleBinding("product", "Product", @"C:\src\Product.cs", 1, 4),
                        new RoleBinding("collection", "List_string_", null, null, null)
                    ])
            ]),
            new PatternReport("Singleton",
            [
                new MatchReport([new RoleBinding("class", "S", null, null, null)])
            ])
        ]);

    private static JsonDocument WriteSample()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            SarifReportWriter.Write(path, SampleReport());
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Writes_the_sarif_2_1_0_envelope()
    {
        using var sarif = WriteSample();
        var root = sarif.RootElement;

        Assert.Equal("https://json.schemastore.org/sarif-2.1.0.json", root.GetProperty("$schema").GetString());
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());

        var driver = root.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver");
        Assert.Equal("DesignPatternDetection", driver.GetProperty("name").GetString());
        Assert.Equal("1.0.0", driver.GetProperty("version").GetString());

        var rules = driver.GetProperty("rules");
        Assert.Equal(2, rules.GetArrayLength());
        Assert.Equal("FactoryMethod", rules[0].GetProperty("id").GetString());
        Assert.Equal("Singleton", rules[1].GetProperty("id").GetString());
    }

    [Fact]
    public void Emits_one_note_result_per_match_with_a_stable_rule_id()
    {
        using var sarif = WriteSample();
        var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results");

        Assert.Equal(2, results.GetArrayLength());
        Assert.Equal("FactoryMethod", results[0].GetProperty("ruleId").GetString());
        Assert.Equal(0, results[0].GetProperty("ruleIndex").GetInt32());
        Assert.Equal("note", results[0].GetProperty("level").GetString());
        Assert.Equal(
            "Factory Method: creator = Creator, product = Product, collection = List_string_",
            results[0].GetProperty("message").GetProperty("text").GetString());
    }

    [Fact]
    public void First_spanned_role_is_the_primary_location_and_the_rest_are_related()
    {
        using var sarif = WriteSample();
        var result = sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        var location = Assert.Single(result.GetProperty("locations").EnumerateArray());
        var physical = location.GetProperty("physicalLocation");
        var uri = physical.GetProperty("artifactLocation").GetProperty("uri").GetString();
        Assert.StartsWith("file:///", uri);
        Assert.EndsWith("Creator.cs", uri);
        Assert.Equal(3, physical.GetProperty("region").GetProperty("startLine").GetInt32());
        Assert.Equal(10, physical.GetProperty("region").GetProperty("endLine").GetInt32());
        Assert.Equal("creator = Creator", location.GetProperty("message").GetProperty("text").GetString());

        var related = Assert.Single(result.GetProperty("relatedLocations").EnumerateArray());
        Assert.EndsWith(
            "Product.cs",
            related.GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString());
    }

    [Fact]
    public void A_match_with_no_spans_emits_a_result_without_locations()
    {
        using var sarif = WriteSample();
        var result = sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[1];

        Assert.Equal("Singleton", result.GetProperty("ruleId").GetString());
        Assert.False(result.TryGetProperty("locations", out _));
        Assert.False(result.TryGetProperty("relatedLocations", out _));
    }
}

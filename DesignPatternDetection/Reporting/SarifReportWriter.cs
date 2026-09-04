using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesignPatternDetection.Reporting;

/// <summary>
/// Writes a <see cref="DetectionReport"/> as SARIF 2.1.0: one rule per detector and one "note"-level result per
/// match, with the reviewer's ruling - when the scan was verified - in the result's property bag and message text.
/// The first role with a span is the result's primary location and the remaining spanned roles are
/// relatedLocations; roles without spans appear only in the message text, and a match with no spanned role emits a
/// result without locations. Artifact URIs are absolute <c>file:///</c> URIs.
/// </summary>
public static class SarifReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Write(string path, DetectionReport report)
    {
        var rules = report.Patterns
            .Select(pattern => new Rule(
                RuleId(pattern.Pattern),
                pattern.Pattern,
                new Message($"{pattern.Pattern} design pattern")))
            .ToList();

        var results = report.Patterns
            .SelectMany((pattern, index) => pattern.Matches.Select(match => ToResult(pattern, index, match)))
            .ToList();

        var log = new SarifLog(
            "https://json.schemastore.org/sarif-2.1.0.json",
            "2.1.0",
            [new Run(new Tool(new Driver(report.Tool, report.Version, rules)), results)]);

        File.WriteAllText(path, JsonSerializer.Serialize(log, JsonOptions));
    }

    private static Result ToResult(PatternReport pattern, int ruleIndex, MatchReport match)
    {
        var spanned = match.Roles
            .Where(role => role.File is not null)
            .Select(role => new Location(
                new PhysicalLocation(
                    new ArtifactLocation(new Uri(Path.GetFullPath(role.File!)).AbsoluteUri),
                    new Region(role.StartLine!.Value, role.EndLine!.Value)),
                new Message($"{role.Role} = {role.Label}")))
            .ToList();

        var text = $"{pattern.Pattern}: {string.Join(", ", match.Roles.Select(role => $"{role.Role} = {role.Label}"))}";

        if (match.Verdict is { } verdict)
            text += $" [{verdict.Outcome}: {verdict.Rationale}]";

        var fdpRoles = match.Roles
            .Where(role => role.Iri is not null)
            .ToDictionary(role => role.Role, role => role.Iri!);

        var properties = match.Verdict is null && fdpRoles.Count == 0
            ? null
            : new ResultProperties(
                match.Verdict?.Outcome,
                match.Verdict?.Rationale,
                match.Verdict?.Model,
                fdpRoles.Count > 0 ? fdpRoles : null);

        return new Result(
            RuleId(pattern.Pattern),
            ruleIndex,
            "note",
            new Message(text),
            spanned.Count > 0 ? [spanned[0]] : null,
            spanned.Count > 1 ? spanned[1..] : null,
            properties);
    }

    /// <summary>A stable rule id: the pattern name without spaces or punctuation.</summary>
    private static string RuleId(string patternName) =>
        new(patternName.Where(char.IsLetterOrDigit).ToArray());

    private sealed record SarifLog(
        [property: JsonPropertyName("$schema")] string Schema,
        string Version,
        IReadOnlyList<Run> Runs);

    private sealed record Run(Tool Tool, IReadOnlyList<Result> Results);

    private sealed record Tool(Driver Driver);

    private sealed record Driver(string Name, string Version, IReadOnlyList<Rule> Rules);

    private sealed record Rule(string Id, string Name, Message ShortDescription);

    private sealed record Message(string Text);

    private sealed record Result(
        string RuleId,
        int RuleIndex,
        string Level,
        Message Message,
        IReadOnlyList<Location>? Locations,
        IReadOnlyList<Location>? RelatedLocations,
        ResultProperties? Properties = null);

    /// <summary>SARIF's property bag, carrying the reviewer's ruling and the FDP role IRIs.</summary>
    private sealed record ResultProperties(
        string? Verdict,
        string? Rationale,
        string? Model,
        IReadOnlyDictionary<string, string>? FdpRoles);

    private sealed record Location(PhysicalLocation PhysicalLocation, Message Message);

    private sealed record PhysicalLocation(ArtifactLocation ArtifactLocation, Region Region);

    private sealed record ArtifactLocation(string Uri);

    private sealed record Region(int StartLine, int EndLine);
}

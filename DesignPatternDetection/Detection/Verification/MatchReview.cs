namespace DesignPatternDetection.Detection.Verification;

/// <summary>Assembles the semantic pass from the settings a command line carries.</summary>
public static class MatchReview
{
    /// <summary>
    /// A verifier that judges each pattern against the rubric in its detector's own XML documentation.
    /// <paramref name="maxParallelism"/> is how many adjudications run at once, null taking the default;
    /// <paramref name="keepRejected"/> records every ruling without removing anything.
    /// </summary>
    public static MatchVerifier Create(
        ILanguageModel model,
        IEnumerable<IPatternDetector> detectors,
        IVerdictCache cache,
        int? maxParallelism = null,
        bool keepRejected = false)
    {
        var options = new VerificationOptions(DropRejected: !keepRejected);

        return new MatchVerifier(
            model,
            XmlDocRubricSource.Load(detectors),
            cache,
            maxParallelism is { } lanes ? options with { MaxParallelism = lanes } : options);
    }
}

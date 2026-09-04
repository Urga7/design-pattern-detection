namespace DesignPatternDetection.Detection.Verification;

/// <summary>One adjudication request.</summary>
/// <param name="SystemPrompt">The pattern preamble, identical for every candidate of the same pattern.</param>
/// <param name="UserPrompt">The candidate's roles and their source text.</param>
/// <param name="JsonSchema">The schema of the expected reply.</param>
public sealed record LanguageModelRequest(string SystemPrompt, string UserPrompt, string JsonSchema);

/// <summary>One adjudication's answer and its token cost. Providers that report no usage return zeros.</summary>
public sealed record LanguageModelReply(string Text, long InputTokens = 0, long OutputTokens = 0);

/// <summary>The seam between this project and whatever model adjudicates its matches: text in, JSON text out.</summary>
/// <remarks>
/// Implementations surface transport and authentication failures - and a truncated answer - as exceptions rather than
/// degraded answers. <see cref="MatchVerifier"/> counts a throw as "unreviewed" and keeps the match.
/// </remarks>
public interface ILanguageModel
{
    /// <summary>The model id, which appears in a <see cref="MatchVerdict"/> and in the verdict cache key.</summary>
    string Name { get; }

    /// <summary>Answers one request with the raw JSON text of the reply.</summary>
    Task<LanguageModelReply> CompleteAsync(LanguageModelRequest request, CancellationToken cancellationToken = default);
}

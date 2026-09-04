using DesignPatternDetection.Detection.Verification.LanguageModelImplementations;

namespace DesignPatternDetection.Detection.Verification;

/// <summary>Picks the <see cref="ILanguageModel"/> for a model id.</summary>
public static class LanguageModels
{
    /// <summary>
    /// The reviewer for <paramref name="modelId"/> - a vendor's own id (<c>claude-opus-5</c>,
    /// <c>gemini-3.7-flash</c>), or null for the default Claude model. Unrecognised ids go to Anthropic.
    /// </summary>
    /// <param name="modelId">The model id, or null for the default reviewer.</param>
    /// <param name="maxTokens">Ceiling on one adjudication, or null for each provider's own default.</param>
    public static ILanguageModel Create(string? modelId, int? maxTokens = null) =>
        modelId is not null && GeminiLanguageModel.Handles(modelId)
            ? new GeminiLanguageModel(modelId, maxTokens: maxTokens ?? GeminiLanguageModel.DefaultMaxTokens)
            : new AnthropicLanguageModel(modelId, maxTokens: maxTokens ?? AnthropicLanguageModel.DefaultMaxTokens);
}

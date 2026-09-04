using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace DesignPatternDetection.Detection.Verification.LanguageModelImplementations;

/// <summary>
/// Claude, through the official Anthropic SDK. The per-pattern preamble goes in <c>System</c> behind a cache
/// breakpoint, and the reply is constrained by JSON Schema.
/// </summary>
public sealed class AnthropicLanguageModel : ILanguageModel
{
    /// <summary>The model used when none is named.</summary>
    public const string DefaultModel = "claude-opus-5";

    /// <summary>Ceiling on reasoning plus reply for one adjudication.</summary>
    public const int DefaultMaxTokens = 8000;

    private static readonly JsonSerializerOptions SchemaOptions = new();

    private readonly AnthropicClient _client;
    private readonly int _maxTokens;

    /// <param name="model">Model id; defaults to <see cref="DefaultModel"/>.</param>
    /// <param name="apiKey">
    /// Explicit key, or null to let the SDK resolve one from the environment (<c>ANTHROPIC_API_KEY</c>, or a
    /// signed-in profile).
    /// </param>
    /// <param name="maxTokens">Ceiling on reasoning plus reply for one adjudication.</param>
    public AnthropicLanguageModel(string? model = null, string? apiKey = null, int maxTokens = DefaultMaxTokens)
    {
        Name = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        _maxTokens = maxTokens;
        _client = apiKey is { Length: > 0 } ? new AnthropicClient { ApiKey = apiKey } : new AnthropicClient();
    }

    public string Name { get; }

    public async Task<LanguageModelReply> CompleteAsync(
        LanguageModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new MessageCreateParams
        {
            Model = Name,
            MaxTokens = _maxTokens,
            System = new List<TextBlockParam>
            {
                new() { Text = request.SystemPrompt, CacheControl = new CacheControlEphemeral() }
            },
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = Schema(request.JsonSchema) }
            },
            Messages = [new MessageParam { Role = Role.User, Content = request.UserPrompt }]
        };

        var message = await _client.Messages.Create(parameters, cancellationToken: cancellationToken);

        // A safety decline is a successful HTTP call with no answer in it.
        if (message.StopReason == "refusal")
        {
            throw new InvalidOperationException(
                $"The reviewer declined to answer ({message.StopDetails?.Category ?? "unspecified"}).");
        }

        // A reply stopped by the token ceiling is truncated JSON.
        if (message.StopReason == "max_tokens")
            throw new InvalidOperationException($"The reviewer's reply hit the {_maxTokens}-token ceiling.");

        var text = string.Concat(
            message.Content
                .Select(block => block.Value)
                .OfType<TextBlock>()
                .Select(block => block.Text));

        return text.Length > 0
            ? new LanguageModelReply(text, message.Usage.InputTokens, message.Usage.OutputTokens)
            : throw new InvalidOperationException($"The reviewer returned no text (stop reason: {message.StopReason}).");
    }

    private static Dictionary<string, JsonElement> Schema(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, SchemaOptions)
        ?? throw new ArgumentException("The verdict schema is not a JSON object.", nameof(json));
}

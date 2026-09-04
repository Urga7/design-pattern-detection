using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DesignPatternDetection.Detection.Verification.LanguageModelImplementations;

public sealed class GeminiLanguageModel : ILanguageModel
{
    public const string DefaultModel = "gemini-3.7-flash";
    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    public const int DefaultMaxTokens = 16000;
    public const string ApiKeyVariable = "GEMINI_API_KEY";

    /// <summary>Checked when <see cref="ApiKeyVariable"/> is unset.</summary>
    public const string FallbackApiKeyVariable = "GOOGLE_API_KEY";

    /// <summary>How many times a rate-limited request is retried before it fails.</summary>
    private const int RateLimitRetries = 3;

    /// <summary>Ceiling on one honoured <c>Retry-After</c>.</summary>
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>JSON Schema keywords that <c>responseSchema</c> does not accept.</summary>
    private static readonly HashSet<string> UnsupportedSchemaKeywords = new(StringComparer.Ordinal)
    {
        "additionalProperties", "$schema", "$id", "$ref", "$defs", "definitions",
        "oneOf", "allOf", "not", "patternProperties", "unevaluatedProperties"
    };

    /// <summary>Shared by every instance; the per-request header carries the only instance-specific state.</summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string? _apiKey;
    private readonly string _endpoint;
    private readonly int? _thinkingBudget;
    private readonly int _maxTokens;
    private readonly HttpClient _http;

    /// <summary>Set once the provider has rejected the response schema; the rest of the run asks for plain JSON.</summary>
    private volatile bool _schemaUnsupported;

    /// <summary>Set once the provider has rejected this model's thinking configuration.</summary>
    private volatile bool _thinkingUnsupported;

    /// <param name="model">
    /// Model id. <c>gemini</c> or null resolve to <see cref="DefaultModel"/>. A leading <c>models/</c> is stripped.
    /// </param>
    /// <param name="apiKey">
    /// Explicit key, or null to read <see cref="ApiKeyVariable"/> - then <see cref="FallbackApiKeyVariable"/>.
    /// </param>
    /// <param name="baseUrl">API root; null takes <see cref="DefaultBaseUrl"/>.</param>
    /// <param name="maxTokens">Ceiling on one adjudication, defaulting to <see cref="DefaultMaxTokens"/>.</param>
    /// <param name="thinkingBudget">
    /// Thinking tokens allowed: <c>0</c> disables thinking, <c>-1</c> lets the model decide. Null takes the model's
    /// default.
    /// </param>
    /// <param name="httpClient">Transport override.</param>
    public GeminiLanguageModel(
        string? model = null,
        string? apiKey = null,
        string? baseUrl = null,
        int maxTokens = DefaultMaxTokens,
        int? thinkingBudget = null,
        HttpClient? httpClient = null)
    {
        Name = WireModel(model);
        _thinkingBudget = thinkingBudget ?? DefaultThinkingBudget(Name);
        _maxTokens = maxTokens;
        _http = httpClient ?? Http;

        var root = (string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl).TrimEnd('/');
        _endpoint = $"{root}/models/{Name}:generateContent";

        // A missing key surfaces on the first adjudication rather than at construction.
        _apiKey = apiKey is { Length: > 0 }
            ? apiKey
            : Environment.GetEnvironmentVariable(ApiKeyVariable)
              ?? Environment.GetEnvironmentVariable(FallbackApiKeyVariable);
    }

    public string Name { get; }

    public static bool Handles(string modelId) =>
        modelId.StartsWith("gemini", StringComparison.OrdinalIgnoreCase)
        || modelId.StartsWith("models/gemini", StringComparison.OrdinalIgnoreCase);

    public async Task<LanguageModelReply> CompleteAsync(
        LanguageModelRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_apiKey is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                $"No Gemini API key. Set {ApiKeyVariable} or pass one explicitly.");
        }

        var useSchema = !_schemaUnsupported;
        var useThinking = !_thinkingUnsupported && _thinkingBudget is not null;
        var rateLimited = 0;

        while (true)
        {
            using var response = await SendAsync(request, useSchema, useThinking, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                return Read(payload);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                // Both fields have moved between model generations.
                if (useThinking && payload.Contains("thinking", StringComparison.OrdinalIgnoreCase))
                {
                    useThinking = false;
                    _thinkingUnsupported = true;
                    continue;
                }

                if (useSchema && payload.Contains("schema", StringComparison.OrdinalIgnoreCase))
                {
                    useSchema = false;
                    _schemaUnsupported = true;
                    continue;
                }
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && rateLimited < RateLimitRetries)
            {
                rateLimited++;
                await Task.Delay(RetryDelay(response, rateLimited), cancellationToken);
                continue;
            }

            throw new HttpRequestException($"Gemini returned {(int)response.StatusCode}: {Describe(payload)}");
        }
    }

    private Task<HttpResponseMessage> SendAsync(
        LanguageModelRequest request,
        bool useSchema,
        bool useThinking,
        CancellationToken cancellationToken)
    {
        var body = new GenerateRequest(
            new Content([new Part(request.SystemPrompt + "\n\n" + SchemaInstruction(request.JsonSchema))]),
            [new Content([new Part(request.UserPrompt)], "user")],
            new GenerationConfig(
                _maxTokens,
                "application/json",
                useSchema ? ResponseSchema(request.JsonSchema) : null,
                useThinking ? new ThinkingConfig(_thinkingBudget!.Value) : null));

        var message = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        
        message.Headers.Add("x-goog-api-key", _apiKey);
        
        return _http.SendAsync(message, cancellationToken);
    }

    private LanguageModelReply Read(string payload)
    {
        var completion = JsonSerializer.Deserialize<GenerateResponse>(payload, JsonOptions);

        // A prompt refused up front comes back with no candidates at all.
        var candidate = completion?.Candidates?.FirstOrDefault()
                        ?? throw new InvalidOperationException(
                            completion?.PromptFeedback?.BlockReason is { Length: > 0 } blocked
                                ? $"Gemini blocked the prompt ({blocked})."
                                : "Gemini returned no candidates.");

        // A reply stopped by the token ceiling is truncated JSON.
        if (candidate.FinishReason == "MAX_TOKENS")
            throw new InvalidOperationException($"The reviewer's reply hit the {_maxTokens}-token ceiling.");

        // Anything other than a clean stop.
        if (candidate.FinishReason is { Length: > 0 } reason && reason != "STOP")
            throw new InvalidOperationException($"The reviewer stopped without answering ({reason}).");

        var text = string.Concat(
            (candidate.Content?.Parts ?? [])
                .Select(part => part.Text)
                .Where(part => part is { Length: > 0 }));

        return text.Length > 0
            ? new LanguageModelReply(
                text,
                completion.UsageMetadata?.PromptTokenCount ?? 0,
                // Thinking is billed as output.
                (completion.UsageMetadata?.CandidatesTokenCount ?? 0)
                + (completion.UsageMetadata?.ThoughtsTokenCount ?? 0))
            : throw new InvalidOperationException(
                $"Gemini returned no text (finish reason: {candidate.FinishReason ?? "none"}).");
    }

    /// <summary>
    /// The id as the API addresses it: any <c>models/</c> qualifier removed, and <see cref="DefaultModel"/> when
    /// nothing else is named.
    /// </summary>
    private static string WireModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return DefaultModel;

        var id = model.Trim();

        if (id.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            id = id["models/".Length..].Trim();

        return id.Length == 0 || id.Equals("gemini", StringComparison.OrdinalIgnoreCase) ? DefaultModel : id;
    }

    /// <summary>Zero on Flash, unset elsewhere - only some models accept the field.</summary>
    private static int? DefaultThinkingBudget(string wireModel) =>
        wireModel.Contains("flash", StringComparison.OrdinalIgnoreCase) ? 0 : null;

    /// <summary>
    /// The verdict schema as <c>responseSchema</c> accepts it - a subset of OpenAPI 3.0, not JSON Schema - with
    /// unsupported keywords pruned by name at every depth.
    /// </summary>
    private static JsonNode ResponseSchema(string json) =>
        Prune(JsonNode.Parse(json)) as JsonObject ??
        throw new ArgumentException("The verdict schema is not a JSON object.", nameof(json));

    private static JsonNode? Prune(JsonNode? node) => node switch
    {
        JsonObject o => Prune(o),
        JsonArray a => Prune(a),
        _ => node?.DeepClone()
    };

    private static JsonObject Prune(JsonObject source)
    {
        var pruned = new JsonObject();
        foreach (var (key, value) in source)
            if (!UnsupportedSchemaKeywords.Contains(key))
                pruned[key] = Prune(value);

        return pruned;
    }

    private static JsonArray Prune(JsonArray source)
    {
        var pruned = new JsonArray();
        foreach (var item in source)
            pruned.Add(Prune(item));

        return pruned;
    }

    /// <summary>
    /// The delay the provider asked for, or a short backoff when it named none, capped at
    /// <see cref="MaxRetryDelay"/>.
    /// </summary>
    private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
    {
        var requested = response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - DateTimeOffset.UtcNow,
            _ => TimeSpan.FromSeconds(attempt)
        };

        return requested < TimeSpan.Zero ? TimeSpan.Zero
            : requested > MaxRetryDelay ? MaxRetryDelay
            : requested;
    }

    /// <summary>Restates the reply contract in the prompt, for when the response schema is unavailable.</summary>
    private static string SchemaInstruction(string schema) =>
        $"Reply with a single JSON object and nothing else. It must satisfy this schema:\n{schema}";

    /// <summary>Pulls the message out of an error body, falling back to the body.</summary>
    private static string Describe(string payload)
    {
        try
        {
            if (JsonSerializer.Deserialize<ErrorResponse>(payload, JsonOptions)?.Error?.Message is { Length: > 0 } message)
                return message;
        }
        catch (JsonException)
        {
            // Not JSON: the raw body is returned instead.
        }

        return payload.Length > 400 ? payload[..400] : payload;
    }

    private sealed record GenerateRequest(
        Content SystemInstruction,
        IReadOnlyList<Content> Contents,
        GenerationConfig GenerationConfig);

    private sealed record Content(IReadOnlyList<Part>? Parts, string? Role = null);

    private sealed record Part(string? Text);

    private sealed record GenerationConfig(
        int MaxOutputTokens,
        string ResponseMimeType,
        JsonNode? ResponseSchema,
        ThinkingConfig? ThinkingConfig);

    private sealed record ThinkingConfig(int ThinkingBudget);

    private sealed record GenerateResponse(
        IReadOnlyList<Candidate>? Candidates,
        PromptFeedback? PromptFeedback,
        UsageMetadata? UsageMetadata);

    private sealed record Candidate(Content? Content, string? FinishReason);

    private sealed record PromptFeedback(string? BlockReason);

    /// <summary>Gemini reports thinking separately from the answer, and bills both as output.</summary>
    private sealed record UsageMetadata(long PromptTokenCount, long CandidatesTokenCount, long ThoughtsTokenCount);

    private sealed record ErrorResponse(ErrorDetail? Error);

    private sealed record ErrorDetail(int Code, string? Message, string? Status);
}

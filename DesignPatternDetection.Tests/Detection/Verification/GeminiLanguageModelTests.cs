using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DesignPatternDetection.Detection.Verification;
using DesignPatternDetection.Detection.Verification.LanguageModelImplementations;

namespace DesignPatternDetection.Tests.Detection.Verification;

/// <summary>
/// Pins the wire format against Google Generative Language API - <c>systemInstruction</c> and <c>contents</c>,
/// camelCase, an OpenAPI-subset <c>responseSchema</c> - none of which can be exercised against the real endpoint in
/// CI.
/// </summary>
public class GeminiLanguageModelTests
{
    private const string Schema =
        """
        {"type":"object","properties":{"verdict":{"type":"string","enum":["confirmed","rejected"]}},"required":["verdict"],"additionalProperties":false}
        """;

    private static readonly LanguageModelRequest Request =
        new("Judge the Decorator.", "component = Wrapper", Schema);

    [Fact]
    public async Task Posts_to_the_generate_content_endpoint_for_the_named_model()
    {
        var stub = new StubHandler(Reply("{}"));
        await Model(stub).CompleteAsync(Request);

        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent",
            stub.RequestUri!.ToString());
    }

    /// <summary>The documented alternative, a <c>?key=</c> query parameter, would put the credential in logs.</summary>
    [Fact]
    public async Task Sends_the_key_as_a_header_and_never_in_the_url()
    {
        var stub = new StubHandler(Reply("{}"));
        await Model(stub).CompleteAsync(Request);

        Assert.Equal("test-key", stub.ApiKey);
        Assert.DoesNotContain("test-key", stub.RequestUri!.ToString());
    }

    [Fact]
    public async Task Sends_the_prompts_in_geminis_own_envelope()
    {
        var stub = new StubHandler(Reply("{}"));
        await Model(stub).CompleteAsync(Request);

        var body = stub.Body();

        var system = body.GetProperty("systemInstruction").GetProperty("parts")
            .EnumerateArray().First().GetProperty("text").GetString()!;
        Assert.Contains("Judge the Decorator.", system);

        var contents = body.GetProperty("contents").EnumerateArray().ToList();
        Assert.Single(contents);
        Assert.Equal("user", contents[0].GetProperty("role").GetString());
        Assert.Equal(
            "component = Wrapper",
            contents[0].GetProperty("parts").EnumerateArray().First().GetProperty("text").GetString());

        Assert.Equal(
            GeminiLanguageModel.DefaultMaxTokens,
            body.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32());
    }

    /// <summary>
    /// <c>responseSchema</c> takes a subset of OpenAPI 3.0, not JSON Schema, and the verdict contract carries
    /// <c>additionalProperties</c>; sending it unpruned is a 400 on every adjudication.
    /// </summary>
    [Fact]
    public async Task Prunes_schema_keywords_the_api_does_not_accept()
    {
        var stub = new StubHandler(Reply("{}"));
        await Model(stub).CompleteAsync(Request);

        var config = stub.Body().GetProperty("generationConfig");

        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());

        var schema = config.GetProperty("responseSchema");
        Assert.False(schema.TryGetProperty("additionalProperties", out _));

        // Everything the subset does support has to survive the pruning.
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal("verdict", schema.GetProperty("required").EnumerateArray().First().GetString());

        var verdict = schema.GetProperty("properties").GetProperty("verdict");
        Assert.Equal("string", verdict.GetProperty("type").GetString());
        Assert.Equal(2, verdict.GetProperty("enum").GetArrayLength());
    }

    /// <summary>Thinking is billed as output, and a two-field verdict against a stated trait needs none.</summary>
    [Theory]
    [InlineData("gemini-2.5-flash", 0)]
    [InlineData("gemini-flash-latest", 0)]
    [InlineData("gemini-2.5-pro", null)]
    public async Task Disables_thinking_on_flash_and_leaves_other_models_alone(string modelId, int? expected)
    {
        var stub = new StubHandler(Reply("{}"));
        await Model(stub, modelId).CompleteAsync(Request);

        var present = stub.Body().GetProperty("generationConfig")
            .TryGetProperty("thinkingConfig", out var thinking);

        Assert.Equal(expected is not null, present);

        if (expected is not null)
            Assert.Equal(expected, thinking.GetProperty("thinkingBudget").GetInt32());
    }

    [Fact]
    public async Task The_thinking_budget_can_be_overridden()
    {
        var stub = new StubHandler(Reply("{}"));
        await new GeminiLanguageModel("gemini-2.5-flash", "test-key", thinkingBudget: 2048,
                httpClient: new HttpClient(stub))
            .CompleteAsync(Request);

        Assert.Equal(
            2048,
            stub.Body().GetProperty("generationConfig").GetProperty("thinkingConfig")
                .GetProperty("thinkingBudget").GetInt32());
    }

    /// <summary>Both the schema and the thinking config have moved between Gemini generations.</summary>
    [Fact]
    public async Task Drops_the_response_schema_when_the_model_rejects_it()
    {
        var stub = new StubHandler(
            Rejects("Invalid JSON payload received. Unknown name \"responseSchema\"."),
            Reply("""{"verdict": "confirmed"}"""));

        var reply = await Model(stub, "gemini-2.5-pro").CompleteAsync(Request);

        Assert.Equal(2, stub.Calls);
        Assert.True(stub.Body(0).GetProperty("generationConfig").TryGetProperty("responseSchema", out _));

        var retried = stub.Body(1).GetProperty("generationConfig");
        Assert.False(retried.TryGetProperty("responseSchema", out _));

        // Plain JSON mode still constrains the reply, and the prompt still
        // carries the contract.
        Assert.Equal("application/json", retried.GetProperty("responseMimeType").GetString());
        Assert.Contains("confirmed", reply.Text);
    }

    [Fact]
    public async Task Drops_the_thinking_config_when_the_model_rejects_it()
    {
        var stub = new StubHandler(
            Rejects("Budget 0 is invalid: thinking cannot be disabled for this model."),
            Reply("""{"verdict": "confirmed"}"""));

        await Model(stub).CompleteAsync(Request);

        Assert.Equal(2, stub.Calls);
        Assert.False(stub.Body(1).GetProperty("generationConfig").TryGetProperty("thinkingConfig", out _));
    }

    /// <summary>A downgrade is remembered, so a rejected field is not re-sent once per match.</summary>
    [Fact]
    public async Task Stays_downgraded_for_the_rest_of_the_run()
    {
        var stub = new StubHandler(Rejects("Unknown name \"responseSchema\"."), Reply("{}"));
        var model = Model(stub, "gemini-2.5-pro");

        await model.CompleteAsync(Request);
        await model.CompleteAsync(Request);

        Assert.Equal(3, stub.Calls);
        Assert.False(stub.Body(2).GetProperty("generationConfig").TryGetProperty("responseSchema", out _));
    }

    /// <summary>A 400 about any other field is a real failure, not a downgradeable one.</summary>
    [Fact]
    public async Task A_400_unrelated_to_a_downgradeable_field_is_not_retried()
    {
        var stub = new StubHandler(Rejects("API key not valid. Please pass a valid API key."));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Model(stub).CompleteAsync(Request));

        Assert.Equal(1, stub.Calls);
        Assert.Contains("API key not valid", exception.Message);
    }

    /// <summary>Thinking is billed as output, so the thought tokens belong in the reported output cost.</summary>
    [Fact]
    public async Task Reports_thought_tokens_as_part_of_the_output_cost()
    {
        var stub = new StubHandler(Reply("""{"verdict": "confirmed", "rationale": "ok"}"""));

        var reply = await Model(stub).CompleteAsync(Request);

        Assert.Contains("confirmed", reply.Text);
        Assert.Equal(1234, reply.InputTokens);
        Assert.Equal(56 + 200, reply.OutputTokens);
    }

    /// <summary>A truncated reply is not a verdict, so the verifier must record the match as unreviewed.</summary>
    [Fact]
    public async Task A_reply_cut_off_by_the_token_ceiling_fails_rather_than_parsing()
    {
        var stub = new StubHandler(Reply("""{"verdict": "conf""", finishReason: "MAX_TOKENS"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Model(stub).CompleteAsync(Request));

        Assert.Contains("ceiling", exception.Message);
    }

    /// <summary>A filtered answer is an absence of judgment, not an uncertain ruling.</summary>
    [Theory]
    [InlineData("SAFETY")]
    [InlineData("RECITATION")]
    [InlineData("PROHIBITED_CONTENT")]
    public async Task A_reply_stopped_for_any_other_reason_fails(string finishReason)
    {
        var stub = new StubHandler(Reply("", finishReason));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Model(stub).CompleteAsync(Request));

        Assert.Contains(finishReason, exception.Message);
    }

    /// <summary>A prompt refused up front comes back with no candidates at all.</summary>
    [Fact]
    public async Task A_blocked_prompt_reports_why_rather_than_reporting_no_text()
    {
        var stub = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"promptFeedback": {"blockReason": "SAFETY"}}""",
                Encoding.UTF8,
                "application/json")
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Model(stub).CompleteAsync(Request));

        Assert.Contains("SAFETY", exception.Message);
    }

    /// <summary>Free-tier requests per minute are low enough that a raised --verify-parallelism will hit them.</summary>
    [Fact]
    public async Task A_rate_limited_request_is_retried()
    {
        var stub = new StubHandler(RateLimited(), Reply("""{"verdict": "confirmed"}"""));

        var reply = await Model(stub).CompleteAsync(Request);

        Assert.Equal(2, stub.Calls);
        Assert.Contains("confirmed", reply.Text);
    }

    [Fact]
    public async Task A_persistently_rate_limited_request_fails_rather_than_looping()
    {
        var stub = new StubHandler(RateLimited());

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Model(stub).CompleteAsync(Request));

        Assert.Contains("429", exception.Message);
        Assert.Equal(4, stub.Calls); // the first attempt plus three retries
    }

    [Fact]
    public async Task An_api_error_surfaces_the_providers_message()
    {
        var stub = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(
                """{"error": {"code": 403, "message": "Permission denied", "status": "PERMISSION_DENIED"}}""")
        });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Model(stub).CompleteAsync(Request));

        Assert.Contains("403", exception.Message);
        Assert.Contains("Permission denied", exception.Message);
    }

    /// <summary>
    /// A missing key fails on use, not on construction, so the verifier counts the match as unreviewed and the scan
    /// still reports its results.
    /// </summary>
    [Fact]
    public async Task A_missing_key_fails_the_review_not_the_scan()
    {
        using var _ = new Keys(null, null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GeminiLanguageModel().CompleteAsync(Request));

        Assert.Contains(GeminiLanguageModel.ApiKeyVariable, exception.Message);
    }

    /// <summary>Either variable supplies the key, matching what Google tooling accepts.</summary>
    [Fact]
    public async Task The_google_variable_is_honoured_when_the_gemini_one_is_unset()
    {
        using var _ = new Keys(null, "from-google-variable");

        var stub = new StubHandler(Reply("{}"));
        await new GeminiLanguageModel("gemini-2.5-flash", httpClient: new HttpClient(stub)).CompleteAsync(Request);

        Assert.Equal("from-google-variable", stub.ApiKey);
    }

    [Theory]
    [InlineData("gemini-2.5-flash", "gemini-2.5-flash")]
    [InlineData("models/gemini-2.5-flash", "gemini-2.5-flash")]
    [InlineData("gemini", GeminiLanguageModel.DefaultModel)]
    [InlineData(null, GeminiLanguageModel.DefaultModel)]
    public void The_qualifier_is_stripped_from_the_addressed_model(string? modelId, string expected) =>
        Assert.Equal(expected, new GeminiLanguageModel(modelId, "key").Name);

    private static GeminiLanguageModel Model(StubHandler stub, string modelId = "gemini-2.5-flash") =>
        new(modelId, "test-key", httpClient: new HttpClient(stub));

    /// <summary>
    /// Responses are built per call rather than shared: the adapter disposes each one, so a retry handed the same
    /// instance twice would read a disposed body.
    /// </summary>
    private static Func<HttpResponseMessage> Reply(string text, string finishReason = "STOP") =>
        () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new { role = "model", parts = new[] { new { text } } },
                            finishReason
                        }
                    },
                    usageMetadata = new
                    {
                        promptTokenCount = 1234,
                        candidatesTokenCount = 56,
                        thoughtsTokenCount = 200,
                        totalTokenCount = 1490
                    }
                }),
                Encoding.UTF8,
                "application/json")
        };

    private static Func<HttpResponseMessage> Rejects(string message) =>
        () => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { error = new { code = 400, message, status = "INVALID_ARGUMENT" } }),
                Encoding.UTF8,
                "application/json")
        };

    /// <summary>Retry-After zero keeps the retry test instant.</summary>
    private static Func<HttpResponseMessage> RateLimited() =>
        () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"error": {"code": 429, "message": "Quota exceeded"}}""")
            };

            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);

            return response;
        };

    /// <summary>Swaps both key variables for the duration of a test and restores them after.</summary>
    private sealed class Keys : IDisposable
    {
        private readonly string? _gemini = Environment.GetEnvironmentVariable(GeminiLanguageModel.ApiKeyVariable);
        private readonly string? _google = Environment.GetEnvironmentVariable(GeminiLanguageModel.FallbackApiKeyVariable);

        public Keys(string? gemini, string? google)
        {
            Environment.SetEnvironmentVariable(GeminiLanguageModel.ApiKeyVariable, gemini);
            Environment.SetEnvironmentVariable(GeminiLanguageModel.FallbackApiKeyVariable, google);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(GeminiLanguageModel.ApiKeyVariable, _gemini);
            Environment.SetEnvironmentVariable(GeminiLanguageModel.FallbackApiKeyVariable, _google);
        }
    }

    /// <summary>
    /// Replies in order, repeating the last one once the script runs out, and keeps every request body so a retry can
    /// be asserted against the request that provoked it.
    /// </summary>
    private sealed class StubHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new(responses);
        private readonly List<string> _bodies = [];

        public Uri? RequestUri { get; private set; }

        public string? ApiKey { get; private set; }

        public int Calls => _bodies.Count;

        public JsonElement Body(int call = 0) => JsonDocument.Parse(_bodies[call]).RootElement;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.FirstOrDefault() : null;
            _bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));

            return (_responses.Count > 1 ? _responses.Dequeue() : _responses.Peek())();
        }
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace PermissionAwareKnowledge;

public sealed record FoundryRuntimeOptions(
    Uri KnowledgeEndpoint,
    Uri? ModelEndpoint,
    string KnowledgeTokenScope,
    string ModelTokenScope)
{
    public static FoundryRuntimeOptions FromEnvironment()
    {
        var knowledgeEndpoint = RequireUri("FOUNDRY_IQ_ENDPOINT");
        var modelEndpoint = OptionalUri("FOUNDRY_MODEL_ENDPOINT");
        var knowledgeTokenScope = Environment.GetEnvironmentVariable(
            "FOUNDRY_IQ_TOKEN_SCOPE");
        var modelTokenScope = Environment.GetEnvironmentVariable(
            "FOUNDRY_MODEL_TOKEN_SCOPE");

        return new FoundryRuntimeOptions(
            knowledgeEndpoint,
            modelEndpoint,
            string.IsNullOrWhiteSpace(knowledgeTokenScope)
                ? "https://ai.azure.com/.default"
                : knowledgeTokenScope,
            string.IsNullOrWhiteSpace(modelTokenScope)
                ? "https://cognitiveservices.azure.com/.default"
                : modelTokenScope);
    }

    private static Uri RequireUri(string name) =>
        OptionalUri(name) ?? throw new InvalidOperationException(
            $"{name} must be set when --provider foundry is used.");

    private static Uri? OptionalUri(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException($"{name} must be an absolute URI.");
    }
}

public sealed class FoundryKnowledgeSource(
    HttpClient httpClient,
    TokenCredential credential,
    FoundryRuntimeOptions options) : IDocumentSource
{
    public async Task<IReadOnlyList<KnowledgeDocument>> GetDocumentsAsync(
        KnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, options.KnowledgeEndpoint);
        await AuthorizeAsync(message, cancellationToken);
        message.Content = JsonContent.Create(new
        {
            query = request.Question,
            authorizationFilter = new
            {
                allowedGroups = request.CallerGroups
            },
            top = 10
        });

        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<KnowledgeResponse>(
            cancellationToken: cancellationToken);

        return payload?.Documents ?? [];
    }

    private async Task AuthorizeAsync(
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(
            new TokenRequestContext([options.KnowledgeTokenScope]),
            cancellationToken);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private sealed record KnowledgeResponse(IReadOnlyList<KnowledgeDocument> Documents);
}

public sealed class FoundryModelAnswerGenerator(
    HttpClient httpClient,
    TokenCredential credential,
    FoundryRuntimeOptions options) : IAnswerGenerator
{
    public async Task<GeneratedAnswer> GenerateAsync(
        KnowledgeRequest request,
        IReadOnlyList<RankedEvidence> evidence,
        CancellationToken cancellationToken)
    {
        if (options.ModelEndpoint is null)
        {
            return await new DeterministicAnswerGenerator().GenerateAsync(
                request,
                evidence,
                cancellationToken);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, options.ModelEndpoint);
        var token = await credential.GetTokenAsync(
            new TokenRequestContext([options.ModelTokenScope]),
            cancellationToken);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        message.Content = JsonContent.Create(new
        {
            question = request.Question,
            instruction = "Answer only from the supplied evidence. Return JSON with answer and citedDocumentIds.",
            evidence = evidence.Select(item => new
            {
                documentId = item.Document.Id,
                title = item.Document.Title,
                passage = item.Passage
            })
        });

        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var generated = await response.Content.ReadFromJsonAsync<GeneratedAnswer>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        return generated ?? throw new InvalidDataException(
            "The configured Foundry model endpoint returned no answer payload.");
    }
}

public static class FoundryRuntimeFactory
{
    public static DeterministicKnowledgeEngine Create()
    {
        var options = FoundryRuntimeOptions.FromEnvironment();
        var credential = new DefaultAzureCredential();
        var httpClient = new HttpClient();
        return new DeterministicKnowledgeEngine(
            new FoundryKnowledgeSource(httpClient, credential, options),
            new FoundryModelAnswerGenerator(httpClient, credential, options));
    }
}

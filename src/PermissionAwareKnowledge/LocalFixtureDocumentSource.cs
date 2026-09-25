using System.Text.Json;

namespace PermissionAwareKnowledge;

public sealed class LocalFixtureDocumentSource(string fixturePath) : IDocumentSource
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<KnowledgeDocument>> GetDocumentsAsync(
        KnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(fixturePath);
        var documents = await JsonSerializer.DeserializeAsync<List<KnowledgeDocument>>(
            stream,
            SerializerOptions,
            cancellationToken);

        return documents ?? throw new InvalidDataException(
            $"Fixture '{fixturePath}' did not contain a document array.");
    }
}

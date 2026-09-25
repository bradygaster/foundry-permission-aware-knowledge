namespace PermissionAwareKnowledge;

public enum AnswerStatus
{
    Answered,
    InsufficientEvidence
}

public sealed record KnowledgeDocument(
    string Id,
    string Title,
    string Uri,
    IReadOnlyList<string> AllowedGroups,
    string Content);

public sealed record KnowledgeRequest(
    string Question,
    IReadOnlyList<string> CallerGroups);

public sealed record RankedEvidence(
    KnowledgeDocument Document,
    int Score,
    string Passage);

public sealed record Citation(
    string DocumentId,
    string Title,
    string Uri,
    string Quote);

public sealed record KnowledgeAnswer(
    AnswerStatus Status,
    string Answer,
    IReadOnlyList<Citation> Citations,
    int AuthorizedDocumentCount,
    string Reason);

public interface IDocumentSource
{
    Task<IReadOnlyList<KnowledgeDocument>> GetDocumentsAsync(
        KnowledgeRequest request,
        CancellationToken cancellationToken);
}

public interface IAnswerGenerator
{
    Task<GeneratedAnswer> GenerateAsync(
        KnowledgeRequest request,
        IReadOnlyList<RankedEvidence> evidence,
        CancellationToken cancellationToken);
}

public sealed record GeneratedAnswer(
    string Text,
    IReadOnlyList<string> CitedDocumentIds);

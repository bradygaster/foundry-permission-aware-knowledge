namespace PermissionAwareKnowledge;

public sealed class DeterministicAnswerGenerator : IAnswerGenerator
{
    public Task<GeneratedAnswer> GenerateAsync(
        KnowledgeRequest request,
        IReadOnlyList<RankedEvidence> evidence,
        CancellationToken cancellationToken)
    {
        var strongest = evidence[0];
        return Task.FromResult(new GeneratedAnswer(
            strongest.Passage,
            [strongest.Document.Id]));
    }
}

using System.Text.RegularExpressions;

namespace PermissionAwareKnowledge;

public sealed partial class DeterministicKnowledgeEngine(
    IDocumentSource documentSource,
    IAnswerGenerator answerGenerator)
{
    private const int MinimumEvidenceScore = 2;
    private const int MaximumEvidenceDocuments = 3;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "for", "from", "how", "in", "is", "of", "on",
        "the", "to", "what", "when", "where", "which", "who", "why", "with"
    };

    public async Task<KnowledgeAnswer> AnswerAsync(
        KnowledgeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Question);

        var allDocuments = await documentSource.GetDocumentsAsync(request, cancellationToken);
        var authorizedDocuments = allDocuments
            .Where(document => IsAuthorized(document, request.CallerGroups))
            .ToArray();

        var evidence = RankAuthorizedDocuments(request.Question, authorizedDocuments)
            .Take(MaximumEvidenceDocuments)
            .ToArray();

        if (evidence.Length == 0 || evidence[0].Score < MinimumEvidenceScore)
        {
            return Insufficient(authorizedDocuments.Length);
        }

        var generated = await answerGenerator.GenerateAsync(request, evidence, cancellationToken);
        var evidenceById = evidence.ToDictionary(item => item.Document.Id, StringComparer.Ordinal);
        var citedIds = generated.CitedDocumentIds
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (string.IsNullOrWhiteSpace(generated.Text)
            || citedIds.Length == 0
            || citedIds.Any(id => !evidenceById.ContainsKey(id)))
        {
            return Insufficient(authorizedDocuments.Length);
        }

        var citations = citedIds
            .Select(id =>
            {
                var item = evidenceById[id];
                return new Citation(
                    item.Document.Id,
                    item.Document.Title,
                    item.Document.Uri,
                    item.Passage);
            })
            .ToArray();

        return new KnowledgeAnswer(
            AnswerStatus.Answered,
            generated.Text.Trim(),
            citations,
            authorizedDocuments.Length,
            "Answer is grounded only in authorized evidence.");
    }

    private static KnowledgeAnswer Insufficient(int authorizedDocumentCount) =>
        new(
            AnswerStatus.InsufficientEvidence,
            "I cannot answer from the authorized evidence available.",
            [],
            authorizedDocumentCount,
            "No sufficiently relevant authorized evidence was available.");

    private static bool IsAuthorized(
        KnowledgeDocument document,
        IReadOnlyList<string> callerGroups)
    {
        var groups = callerGroups.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return document.AllowedGroups.Any(group =>
            group.Equals("Everyone", StringComparison.OrdinalIgnoreCase)
            || groups.Contains(group));
    }

    private static IEnumerable<RankedEvidence> RankAuthorizedDocuments(
        string question,
        IReadOnlyList<KnowledgeDocument> authorizedDocuments)
    {
        var queryTokens = Tokenize(question);

        return authorizedDocuments
            .Select(document =>
            {
                var titleTokens = Tokenize(document.Title);
                var contentTokens = Tokenize(document.Content);
                var score = queryTokens.Sum(token =>
                    (titleTokens.Contains(token) ? 2 : 0)
                    + (contentTokens.Contains(token) ? 1 : 0));

                return new RankedEvidence(
                    document,
                    score,
                    SelectBestPassage(document, queryTokens));
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Document.Id, StringComparer.Ordinal);
    }

    private static string SelectBestPassage(
        KnowledgeDocument document,
        IReadOnlySet<string> queryTokens)
    {
        var titleTokens = Tokenize(document.Title);
        var passageTokens = queryTokens
            .Where(token => !titleTokens.Contains(token))
            .ToHashSet(StringComparer.Ordinal);
        if (passageTokens.Count == 0)
        {
            passageTokens = queryTokens.ToHashSet(StringComparer.Ordinal);
        }

        return
        SentenceSplitRegex()
            .Split(document.Content)
            .Select((sentence, index) => new
            {
                Text = sentence.Trim(),
                Score = Tokenize(sentence).Count(passageTokens.Contains),
                Index = index
            })
            .Where(candidate => candidate.Text.Length > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => candidate.Text)
            .FirstOrDefault() ?? document.Content.Trim();
    }

    private static HashSet<string> Tokenize(string value) =>
        WordRegex()
            .Matches(value.ToLowerInvariant())
            .Select(match => NormalizeToken(match.Value))
            .Where(token => token.Length > 1 && !StopWords.Contains(token))
            .ToHashSet(StringComparer.Ordinal);

    private static string NormalizeToken(string token) =>
        token.Length > 3 && token.EndsWith('s')
            ? token[..^1]
            : token;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceSplitRegex();
}

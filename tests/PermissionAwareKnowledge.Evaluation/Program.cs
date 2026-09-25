using PermissionAwareKnowledge;

var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "documents.json");
var tests = new (string Name, Func<Task> Run)[]
{
    ("authorized answer includes exact citation", AuthorizedAnswerIncludesCitation),
    ("unauthorized caller cannot retrieve or cite restricted evidence", PermissionIsolation),
    ("unknown question fails closed", UnknownQuestionFailsClosed),
    ("generator receives only authorized evidence", GeneratorReceivesOnlyAuthorizedEvidence)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} evaluations passed.");
return failures.Count == 0 ? 0 : 1;

async Task AuthorizedAnswerIncludesCitation()
{
    var answer = await CreateLocalEngine().AnswerAsync(new KnowledgeRequest(
        "What triggers the Project Orion rollback?",
        ["Engineering"]));

    Equal(AnswerStatus.Answered, answer.Status);
    Equal(1, answer.Citations.Count);
    Equal("engineering-orion-runbook", answer.Citations[0].DocumentId);
    Equal("fixture://engineering-orion-runbook", answer.Citations[0].Uri);
    Contains("error rate above two percent", answer.Answer);
    Contains(answer.Citations[0].Quote, answer.Answer);
}

async Task PermissionIsolation()
{
    var answer = await CreateLocalEngine().AnswerAsync(new KnowledgeRequest(
        "What triggers the Project Orion rollback?",
        ["Everyone"]));

    Equal(AnswerStatus.InsufficientEvidence, answer.Status);
    Equal(0, answer.Citations.Count);
    DoesNotContain("two percent", answer.Answer);
    DoesNotContain("Orion", answer.Answer);
}

async Task UnknownQuestionFailsClosed()
{
    var answer = await CreateLocalEngine().AnswerAsync(new KnowledgeRequest(
        "Who won the lunar chess tournament?",
        ["Engineering", "Finance", "PeopleOps"]));

    Equal(AnswerStatus.InsufficientEvidence, answer.Status);
    Equal(0, answer.Citations.Count);
}

async Task GeneratorReceivesOnlyAuthorizedEvidence()
{
    var generator = new CapturingGenerator();
    var engine = new DeterministicKnowledgeEngine(
        new InMemorySource(
        [
            new KnowledgeDocument(
                "restricted",
                "Orion rollback secret",
                "fixture://restricted",
                ["Engineering"],
                "Orion rollback secret code is violet."),
            new KnowledgeDocument(
                "public",
                "Orion public overview",
                "fixture://public",
                ["Everyone"],
                "Orion public overview says launch status is published weekly.")
        ]),
        generator);

    var answer = await engine.AnswerAsync(new KnowledgeRequest(
        "What does the Orion public overview say about launch status?",
        ["Everyone"]));

    Equal(AnswerStatus.Answered, answer.Status);
    Equal("public", generator.SeenEvidence.Single().Document.Id);
    DoesNotContain("violet", generator.SeenEvidence.Single().Passage);
}

DeterministicKnowledgeEngine CreateLocalEngine() =>
    new(
        new LocalFixtureDocumentSource(fixturePath),
        new DeterministicAnswerGenerator());

static void Equal<T>(T expected, T actual)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void Contains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected '{actual}' to contain '{expected}'.");
    }
}

static void DoesNotContain(string unexpected, string actual)
{
    if (actual.Contains(unexpected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Did not expect '{actual}' to contain '{unexpected}'.");
    }
}

file sealed class InMemorySource(IReadOnlyList<KnowledgeDocument> documents) : IDocumentSource
{
    public Task<IReadOnlyList<KnowledgeDocument>> GetDocumentsAsync(
        KnowledgeRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(documents);
}

file sealed class CapturingGenerator : IAnswerGenerator
{
    public IReadOnlyList<RankedEvidence> SeenEvidence { get; private set; } = [];

    public Task<GeneratedAnswer> GenerateAsync(
        KnowledgeRequest request,
        IReadOnlyList<RankedEvidence> evidence,
        CancellationToken cancellationToken)
    {
        SeenEvidence = evidence;
        return Task.FromResult(new GeneratedAnswer(
            evidence[0].Passage,
            [evidence[0].Document.Id]));
    }
}

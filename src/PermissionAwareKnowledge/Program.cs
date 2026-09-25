using System.Text.Json;
using System.Text.Json.Serialization;
using PermissionAwareKnowledge;

var arguments = Arguments.Parse(args);
var request = new KnowledgeRequest(arguments.Question, arguments.Groups);
var engine = arguments.Provider.Equals("foundry", StringComparison.OrdinalIgnoreCase)
    ? FoundryRuntimeFactory.Create()
    : new DeterministicKnowledgeEngine(
        new LocalFixtureDocumentSource(arguments.FixturePath),
        new DeterministicAnswerGenerator());

var answer = await engine.AnswerAsync(request);
Console.WriteLine(JsonSerializer.Serialize(answer, new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
}));

internal sealed record Arguments(
    string Question,
    IReadOnlyList<string> Groups,
    string Provider,
    string FixturePath)
{
    public static Arguments Parse(string[] args)
    {
        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "Usage: dotnet run -- --question <text> --groups <group1,group2> "
                + "[--provider local|foundry] [--fixtures <path>]");
            Environment.Exit(0);
        }

        var values = args
            .Select((value, index) => new { value, index })
            .Where(item => item.value.StartsWith("--", StringComparison.Ordinal))
            .ToDictionary(
                item => item.value[2..],
                item => item.index + 1 < args.Length ? args[item.index + 1] : string.Empty,
                StringComparer.OrdinalIgnoreCase);

        if (!values.TryGetValue("question", out var question)
            || string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("--question is required.");
        }

        values.TryGetValue("groups", out var groupList);
        values.TryGetValue("provider", out var provider);
        values.TryGetValue("fixtures", out var fixturePath);

        provider = string.IsNullOrWhiteSpace(provider) ? "local" : provider;
        if (!provider.Equals("local", StringComparison.OrdinalIgnoreCase)
            && !provider.Equals("foundry", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("--provider must be 'local' or 'foundry'.");
        }

        return new Arguments(
            question,
            (groupList ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            provider,
            string.IsNullOrWhiteSpace(fixturePath)
                ? Path.Combine(AppContext.BaseDirectory, "fixtures", "documents.json")
                : fixturePath);
    }
}

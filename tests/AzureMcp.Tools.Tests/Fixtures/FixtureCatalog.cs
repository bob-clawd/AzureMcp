using System.Text.Json;
using AzureMcp.Tools;

namespace AzureMcp.Tools.Tests.Fixtures;

internal static class FixtureCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Lazy<FixtureData> Catalog = new(LoadCore);

    public static FixtureData Load() => Catalog.Value;

    public static string ReadText(params string[] relativeSegments)
        => File.ReadAllText(GetPath(relativeSegments));

    public static string GetPath(params string[] relativeSegments)
    {
        var segments = new string[relativeSegments.Length + 2];
        segments[0] = AppContext.BaseDirectory;
        segments[1] = "Fixtures";
        Array.Copy(relativeSegments, 0, segments, 2, relativeSegments.Length);
        return Path.Combine(segments);
    }

    private static FixtureData LoadCore()
    {
        var tickets = Directory
            .EnumerateFiles(GetPath("Tickets"), "*.json")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(LoadTicket)
            .ToDictionary(ticket => ticket.Id);

        var pullRequests = Directory
            .EnumerateFiles(GetPath("PullRequests"), "*.json")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(LoadPullRequest)
            .ToDictionary(pr => pr.Id);

        return new FixtureData(tickets, pullRequests);
    }

    private static TicketFixtureDocument LoadTicket(string path)
        => JsonSerializer.Deserialize<TicketFixtureDocument>(File.ReadAllText(path), JsonOptions)
           ?? throw new InvalidOperationException($"Could not deserialize ticket fixture: {path}");

    private static PullRequestFixtureDocument LoadPullRequest(string path)
        => JsonSerializer.Deserialize<PullRequestFixtureDocument>(File.ReadAllText(path), JsonOptions)
           ?? throw new InvalidOperationException($"Could not deserialize pull request fixture: {path}");
}

internal sealed class FixtureData(
    IReadOnlyDictionary<int, TicketFixtureDocument> tickets,
    IReadOnlyDictionary<int, PullRequestFixtureDocument> pullRequests)
{
    public IReadOnlyDictionary<int, TicketFixtureDocument> Tickets { get; } = tickets;

    public IReadOnlyDictionary<int, PullRequestFixtureDocument> PullRequests { get; } = pullRequests;

    public TicketFixtureDocument Ticket(int id) => Tickets[id];

    public PullRequestFixtureDocument PullRequest(int id) => PullRequests[id];
}

internal sealed record TicketFixtureDocument
{
    public int Id { get; init; }

    public string? Title { get; init; }

    public string? State { get; init; }

    public string? WorkItemType { get; init; }

    public string? DescriptionText { get; init; }

    public string? AssignedTo { get; init; }

    public int? ParentId { get; init; }

    public IReadOnlyList<int> ChildrenIds { get; init; } = [];

    public IReadOnlyList<string> Branches { get; init; } = [];

    public IReadOnlyList<PullRequestRefFixtureDocument> PullRequestRefs { get; init; } = [];

    public string? ChangedDate { get; init; }

    public Ticket ToTicket() => new()
    {
        Id = Id,
        Title = Title,
        State = State,
        WorkItemType = WorkItemType,
        DescriptionText = DescriptionText,
        AssignedTo = AssignedTo,
        ParentId = ParentId,
        ChildrenIds = ChildrenIds.ToArray(),
        Branches = Branches.ToArray(),
        PullRequests = Array.Empty<PullRequestInfo>(),
        PullRequestRefs = PullRequestRefs
            .Select(static pr => new PullRequestRef(pr.ProjectId, pr.RepositoryId, pr.PullRequestId))
            .ToArray()
    };

    public SearchTicketResult ToSearchResult() => new(Id, Title, State, WorkItemType, ChangedDate);
}

internal sealed record PullRequestRefFixtureDocument(
    string ProjectId,
    string RepositoryId,
    int PullRequestId);

internal sealed record PullRequestFixtureDocument
{
    public int Id { get; init; }

    public string ProjectId { get; init; } = string.Empty;

    public string RepositoryId { get; init; } = string.Empty;

    public string? DescriptionText { get; init; }

    public PullRequestInfo ToPullRequest() => new(Id, DescriptionText);

    public bool Matches(string projectId, string repositoryId, int pullRequestId)
        => pullRequestId == Id
           && string.Equals(ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
           && string.Equals(RepositoryId, repositoryId, StringComparison.OrdinalIgnoreCase);
}

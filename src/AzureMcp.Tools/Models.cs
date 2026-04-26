namespace AzureMcp.Tools;

public sealed record ErrorInfo(
    string Message,
    IReadOnlyDictionary<string, string>? Details = null);

public sealed record PullRequestInfo(
    int Id,
    string? DescriptionText);

public sealed record SearchTicketResult(
    int Id,
    string? Title,
    string? State,
    string? WorkItemType,
    string? ChangedDate);

internal sealed record PullRequestRef(
    string ProjectId,
    string RepositoryId,
    int PullRequestId);

public sealed record Ticket
{
    public int Id { get; init; }

    public string? Title { get; init; }

    public string? State { get; init; }

    public string? WorkItemType { get; init; }

    public string? DescriptionText { get; init; }

    public string? AssignedTo { get; init; }

    public int? ParentId { get; init; }

    public IReadOnlyList<int> ChildrenIds { get; init; } = Array.Empty<int>();

    public IReadOnlyList<string> Branches { get; init; } = Array.Empty<string>();

    public IReadOnlyList<PullRequestInfo> PullRequests { get; init; } = Array.Empty<PullRequestInfo>();

    internal IReadOnlyList<PullRequestRef> PullRequestRefs { get; init; } = Array.Empty<PullRequestRef>();
}

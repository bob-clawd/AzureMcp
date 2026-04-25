using AzureMcp.Tools.WorkItems;

namespace AzureMcp.Tools;

public sealed record ErrorInfo(
    string Message,
    IReadOnlyDictionary<string, string>? Details = null);

public sealed record Ticket(
    int Id,
    string? Title,
    string? State,
    string? WorkItemType,
    string? DescriptionText,
    string? AssignedTo,
    int? ParentId,
    IReadOnlyList<int> ChildrenIds);

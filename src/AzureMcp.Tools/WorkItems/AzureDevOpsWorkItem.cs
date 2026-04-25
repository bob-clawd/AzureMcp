namespace AzureMcp.Tools.WorkItems;

public sealed record AssignedTo(
    string? DisplayName,
    string? UniqueName);

public sealed record AzureDevOpsWorkItem(
    int Id,
    string? Title,
    string? State,
    string? WorkItemType,
    string? DescriptionText,
    AssignedTo? AssignedTo,
    int? ParentWorkItemId,
    IReadOnlyList<int> ChildWorkItemIds);

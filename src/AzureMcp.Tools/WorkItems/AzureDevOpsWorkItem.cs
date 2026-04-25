namespace AzureMcp.Tools.WorkItems;

public sealed record AzureDevOpsAssignedTo(
    string? DisplayName,
    string? UniqueName);

public sealed record AzureDevOpsWorkItem(
    int Id,
    string? Title,
    string? State,
    string? WorkItemType,
    string? DescriptionText,
    AzureDevOpsAssignedTo? AssignedTo,
    int? ParentWorkItemId,
    IReadOnlyList<int> ChildWorkItemIds,
    string? Url);

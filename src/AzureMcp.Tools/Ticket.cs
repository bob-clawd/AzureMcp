using AzureMcp.Tools.WorkItems;

namespace AzureMcp.Tools;

public sealed record Ticket(
    int Id,
    string? Title,
    string? State,
    string? WorkItemType,
    string? DescriptionText,
    string? DescriptionHtml,
    AzureDevOpsAssignedTo? AssignedTo,
    int? ParentWorkItemId,
    IReadOnlyList<int> ChildWorkItemIds,
    IReadOnlyList<int> RelatedWorkItemIds,
    string? Url,
    int? Level = null,
    ErrorInfo? Error = null)
{
    public static Ticket AsError(int workItemId, ErrorInfo error)
        => new(
            Id: workItemId,
            Title: null,
            State: null,
            WorkItemType: null,
            DescriptionText: null,
            DescriptionHtml: null,
            AssignedTo: null,
            ParentWorkItemId: null,
            ChildWorkItemIds: Array.Empty<int>(),
            RelatedWorkItemIds: Array.Empty<int>(),
            Url: null,
            Level: null,
            Error: error);

    public static Ticket FromWorkItem(AzureDevOpsWorkItem workItem, int? level = null)
        => new(
            Id: workItem.Id,
            Title: workItem.Title,
            State: workItem.State,
            WorkItemType: workItem.WorkItemType,
            DescriptionText: workItem.DescriptionText,
            DescriptionHtml: workItem.DescriptionHtml,
            AssignedTo: workItem.AssignedTo,
            ParentWorkItemId: workItem.ParentWorkItemId,
            ChildWorkItemIds: workItem.ChildWorkItemIds,
            RelatedWorkItemIds: workItem.RelatedWorkItemIds,
            Url: workItem.Url,
            Level: level,
            Error: null);
}

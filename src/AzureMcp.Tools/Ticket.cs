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
    string? Url)
{
    public static Ticket FromWorkItem(AzureDevOpsWorkItem workItem)
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
            Url: workItem.Url);
}

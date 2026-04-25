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
    AssignedTo? AssignedTo,
    int? ParentWorkItemId)
{
    public static Ticket FromWorkItem(AzureDevOpsWorkItem workItem)
        => new(
            Id: workItem.Id,
            Title: workItem.Title,
            State: workItem.State,
            WorkItemType: workItem.WorkItemType,
            DescriptionText: workItem.DescriptionText,
            AssignedTo: workItem.AssignedTo,
            ParentWorkItemId: workItem.ParentWorkItemId);
}

using System.ComponentModel;
using AzureMcp.Tools.WorkItems;
using ModelContextProtocol.Server;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Tools;

public sealed record ReadWorkItemResponse(
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
    ErrorInfo? Error = null)
{
    public static ReadWorkItemResponse AsError(int workItemId, ErrorInfo error)
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
            Error: error);
}

public sealed class ReadWorkItemTool(IAzureDevOpsWorkItemClient client, IAzureDevOpsConnectionState connectionState) : Tool
{
    [McpServerTool(Name = "read_work_item", Title = "Read Work Item", ReadOnly = true, Idempotent = true)]
    [Description("Load a single Azure DevOps work item by id and return a structured view with the key fields needed for everyday work.")]
    public async Task<ReadWorkItemResponse> ExecuteAsync(
        [Description("Azure DevOps work item id.")] int workItemId,
        CancellationToken cancellationToken = default)
    {
        if (!connectionState.TryGetRequired(out var connection, out var error))
            return ReadWorkItemResponse.AsError(workItemId, error!);

        var result = await client.ReadWorkItemAsync(connection, workItemId, cancellationToken).ConfigureAwait(false);
        if (result.Error is not null)
            return ReadWorkItemResponse.AsError(workItemId, result.Error);

        var workItem = result.WorkItem!;

        return new ReadWorkItemResponse(
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
}

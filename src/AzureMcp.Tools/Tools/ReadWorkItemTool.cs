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
    ErrorInfo? Error = null,
    IReadOnlyList<string>? MissingEnvironmentVariables = null)
{
    public static ReadWorkItemResponse AsError(int workItemId, ErrorInfo error, IReadOnlyList<string>? missingEnvironmentVariables = null)
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
            Error: error,
            MissingEnvironmentVariables: missingEnvironmentVariables);
}

public sealed class ReadWorkItemTool(IAzureDevOpsWorkItemClient client, IAzureDevOpsConnectionState connectionState) : Tool
{
    [McpServerTool(Name = "read_work_item", Title = "Read Work Item", ReadOnly = true, Idempotent = true)]
    [Description("Load a single Azure DevOps work item by id and return a structured view with the key fields needed for everyday work.")]
    public async Task<ReadWorkItemResponse> ExecuteAsync(
        [Description("Azure DevOps work item id.")] int workItemId,
        CancellationToken cancellationToken = default)
    {
        if (!connectionState.TryGetRequired(out _, out var missing))
            return ReadWorkItemResponse.AsError(workItemId, AzureMcpErrors.MissingConfig(missing), missing);

        var workItem = await client.ReadWorkItemAsync(workItemId, cancellationToken).ConfigureAwait(false);

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

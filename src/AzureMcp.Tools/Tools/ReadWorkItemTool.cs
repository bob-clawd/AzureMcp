using System.ComponentModel;
using AzureMcp.Tools.WorkItems;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools.Tools;

public sealed record ReadWorkItemResponse(
    int Id,
    string? Title,
    string? State,
    string? WorkItemType,
    string? DescriptionText,
    string? DescriptionHtml,
    AzureDevOpsAssignedTo? AssignedTo,
    string? Url);

public sealed class ReadWorkItemTool(IAzureDevOpsWorkItemClient client) : Tool
{
    [McpServerTool(Name = "read_work_item", Title = "Read Work Item", ReadOnly = true, Idempotent = true)]
    [Description("Load a single Azure DevOps work item by id and return a structured view with the key fields needed for everyday work.")]
    public async Task<ReadWorkItemResponse> ExecuteAsync(
        [Description("Azure DevOps work item id.")] int id,
        CancellationToken cancellationToken = default)
    {
        var workItem = await client.ReadWorkItemAsync(id, cancellationToken).ConfigureAwait(false);

        return new ReadWorkItemResponse(
            Id: workItem.Id,
            Title: workItem.Title,
            State: workItem.State,
            WorkItemType: workItem.WorkItemType,
            DescriptionText: workItem.DescriptionText,
            DescriptionHtml: workItem.DescriptionHtml,
            AssignedTo: workItem.AssignedTo,
            Url: workItem.Url);
    }
}

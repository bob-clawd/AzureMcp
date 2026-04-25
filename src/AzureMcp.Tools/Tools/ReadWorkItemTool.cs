using System.ComponentModel;
using AzureMcp.Tools.WorkItems;
using ModelContextProtocol.Server;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Tools;

public sealed class ReadWorkItemTool(IAzureDevOpsWorkItemClient client, IAzureDevOpsConnectionState connectionState) : Tool
{
    [McpServerTool(Name = "read_work_item", Title = "Read Work Item", ReadOnly = true, Idempotent = true)]
    [Description("Load a single Azure DevOps work item by id and return a structured view with the key fields needed for everyday work.")]
    public async Task<Ticket> ExecuteAsync(
        [Description("Azure DevOps work item id.")] int workItemId,
        CancellationToken cancellationToken = default)
    {
        if (!connectionState.TryGetRequired(out var connection, out var error))
            return Ticket.AsError(workItemId, error!);

        var result = await client.ReadWorkItemAsync(connection, workItemId, cancellationToken).ConfigureAwait(false);
        if (result.Error is not null)
            return Ticket.AsError(workItemId, result.Error);

        return Ticket.FromWorkItem(result.WorkItem!);
    }
}

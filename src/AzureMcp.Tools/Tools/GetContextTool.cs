using System.ComponentModel;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.Clients;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools.Tools;

public sealed record GetContextResponse(
    IReadOnlyList<Ticket> Tickets,
    ErrorInfo? Error = null)
{
    public static GetContextResponse AsError(ErrorInfo error)
        => new(Array.Empty<Ticket>(), error);
}

public sealed class GetContextTool(IAzureDevOpsWorkItemClient client, IAzureDevOpsConnectionState connectionState) : Tool
{
    [McpServerTool(Name = "get_context", Title = "Get Work Item Context", ReadOnly = true, Idempotent = true)]
    [Description("Load the Azure DevOps parent chain context for a work item id: walk up to the topmost parent and return the chain ordered from parent to child.")]
    public async Task<GetContextResponse> ExecuteAsync(
        [Description("Azure DevOps work item id anywhere inside the hierarchy.")] int workItemId,
        CancellationToken cancellationToken = default)
    {
        if (!connectionState.TryGetRequired(out var connection, out var error))
            return GetContextResponse.AsError(error!);

        var tickets = new List<Ticket>();
        var cache = new Dictionary<int, Ticket>();
        var currentId = workItemId;
        var visited = new HashSet<int>();

        while (true)
        {
            if (!visited.Add(currentId))
            {
                return GetContextResponse.AsError(new ErrorInfo(
                    "Azure DevOps hierarchy contains a cycle while walking parents.",
                    new Dictionary<string, string> { ["workItemId"] = currentId.ToString() }));
            }

            var result = await LoadAsync(connection, currentId, cache, cancellationToken).ConfigureAwait(false);
            if (result.Error is not null)
                return GetContextResponse.AsError(result.Error);

            var ticket = result.Ticket!;
            tickets.Add(ticket);

            if (ticket.ParentId is null)
                break;

            currentId = ticket.ParentId.Value;
        }

        tickets.Reverse();
        return new GetContextResponse(tickets);
    }

    private async Task<(Ticket? Ticket, ErrorInfo? Error)> LoadAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        IDictionary<int, Ticket> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(workItemId, out var cached))
            return (cached, null);

        var result = await client.ReadWorkItemAsync(connection, workItemId, cancellationToken).ConfigureAwait(false);
        if (result.Ticket is not null)
            cache[workItemId] = result.Ticket;

        return result;
    }
}

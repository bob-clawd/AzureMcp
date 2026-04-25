using System.ComponentModel;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.WorkItems;
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
    [Description("Load the vertical Azure DevOps work item context for a work item id: walk up to the topmost parent, then return the full parent-to-children hierarchy in stable order.")]
    public async Task<GetContextResponse> ExecuteAsync(
        [Description("Azure DevOps work item id anywhere inside the hierarchy.")] int workItemId,
        CancellationToken cancellationToken = default)
    {
        if (!connectionState.TryGetRequired(out var connection, out var error))
            return GetContextResponse.AsError(error!);

        var cache = new Dictionary<int, Ticket>();
        var rootResult = await FindRootAsync(connection, workItemId, cache, cancellationToken).ConfigureAwait(false);
        if (rootResult.Error is not null)
            return GetContextResponse.AsError(rootResult.Error);

        var tickets = new List<Ticket>();
        var visited = new HashSet<int>();

        var traversalResult = await TraverseAsync(
            connection,
            rootResult.Root!.Id,
            cache,
            visited,
            tickets,
            cancellationToken).ConfigureAwait(false);

        if (traversalResult is not null)
            return GetContextResponse.AsError(traversalResult);

        return new GetContextResponse(tickets);
    }

    private async Task<(Ticket? Root, ErrorInfo? Error)> FindRootAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        IDictionary<int, Ticket> cache,
        CancellationToken cancellationToken)
    {
        var currentId = workItemId;
        var visited = new HashSet<int>();

        while (true)
        {
            if (!visited.Add(currentId))
            {
                return (null, new ErrorInfo(
                    "Azure DevOps hierarchy contains a cycle while walking parents.",
                    new Dictionary<string, string> { ["workItemId"] = currentId.ToString() }));
            }

            var result = await LoadAsync(connection, currentId, cache, cancellationToken).ConfigureAwait(false);
            if (result.Error is not null)
                return (null, result.Error);

            var current = result.Ticket!;
            if (current.ParentId is null)
                return (current, null);

            currentId = current.ParentId.Value;
        }
    }

    private async Task<ErrorInfo?> TraverseAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        IDictionary<int, Ticket> cache,
        ISet<int> visited,
        ICollection<Ticket> tickets,
        CancellationToken cancellationToken)
    {
        if (!visited.Add(workItemId))
            return null;

        var result = await LoadAsync(connection, workItemId, cache, cancellationToken).ConfigureAwait(false);
        if (result.Error is not null)
            return result.Error;

        var ticket = result.Ticket!;
        tickets.Add(ticket);

        foreach (var childId in ticket.ChildrenIds)
        {
            var error = await TraverseAsync(connection, childId, cache, visited, tickets, cancellationToken).ConfigureAwait(false);
            if (error is not null)
                return error;
        }

        return null;
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

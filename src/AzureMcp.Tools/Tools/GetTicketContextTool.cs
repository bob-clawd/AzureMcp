using System.ComponentModel;
using System.Text.RegularExpressions;
using AzureMcp.Tools.Clients;
using AzureMcp.Tools.Configuration;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools.Tools;

public sealed class GetTicketContextTool(
    IAzureDevOpsWorkItemClient client,
    IAzureDevOpsConnectionState connectionState) : Tool
{
    private static readonly Regex WorkItemIdPattern = new("^#?(?<id>[1-9][0-9]*)$", RegexOptions.Compiled);

    [McpServerTool(Name = "get_ticket_context", Title = "Get Ticket Context", ReadOnly = true, Idempotent = true)]
    [Description(
        "Single-entry ticket tool. Accepts either a work item id (e.g. '123' or '#123') or a free-text query. " +
        "For free-text queries it performs a search (title hits first, then description hits; newest changed first), " +
        "then loads the full parent-chain context for the first hit.")]
    public async Task<GetContextResponse> ExecuteAsync(
        [Description("Work item id or free-text query.")] string input,
        CancellationToken cancellationToken = default)
    {
        if (!connectionState.TryGetRequired(out var connection, out var error))
            return GetContextResponse.AsError(error!);

        input = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return GetContextResponse.AsError(new ErrorInfo(
                "input is required",
                new Dictionary<string, string> { ["input"] = input }));
        }

        if (TryParseWorkItemId(input, out var workItemId))
            return await LoadContextAsync(connection, workItemId, cancellationToken).ConfigureAwait(false);

        const int top = 20;
        const bool includeClosed = false;
        const bool includeDescription = true;

        var search = await client.SearchWorkItemsAsync(
            connection,
            input,
            top,
            includeClosed,
            includeDescription,
            cancellationToken).ConfigureAwait(false);

        if (search.Error is not null)
            return GetContextResponse.AsError(search.Error);

        var hits = search.Results ?? Array.Empty<SearchTicketResult>();
        if (hits.Count == 0)
        {
            return GetContextResponse.AsError(new ErrorInfo(
                "no work items found",
                new Dictionary<string, string> { ["query"] = input }));
        }

        return await LoadContextAsync(connection, hits[0].Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GetContextResponse> LoadContextAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        CancellationToken cancellationToken)
    {
        var tickets = new List<Ticket>();
        var cache = new Dictionary<int, Ticket>();
        var currentId = workItemId;
        var visited = new HashSet<int>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!visited.Add(currentId))
            {
                return GetContextResponse.AsError(new ErrorInfo(
                    "Azure DevOps hierarchy contains a cycle while walking parents.",
                    new Dictionary<string, string> { ["workItemId"] = currentId.ToString() }));
            }

            var (ticket, error) = await LoadAsync(connection, currentId, cache, cancellationToken).ConfigureAwait(false);
            if (error is not null)
                return GetContextResponse.AsError(error);

            tickets.Add(ticket!);

            if (ticket!.ParentId is null)
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

    private static bool TryParseWorkItemId(string input, out int workItemId)
    {
        workItemId = 0;

        var match = WorkItemIdPattern.Match(input);
        if (!match.Success)
            return false;

        var idText = match.Groups["id"].Value;
        return int.TryParse(idText, out workItemId) && workItemId > 0;
    }
}


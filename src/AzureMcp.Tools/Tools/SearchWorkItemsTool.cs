using System.ComponentModel;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.Clients;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools.Tools;

public sealed record SearchWorkItemsResponse(
    IReadOnlyList<SearchTicketResult> Tickets,
    ErrorInfo? Error = null)
{
    public static SearchWorkItemsResponse AsError(ErrorInfo error)
        => new(Array.Empty<SearchTicketResult>(), error);
}

public sealed class SearchWorkItemsTool(
    IAzureDevOpsWorkItemClient client,
    IAzureDevOpsConnectionState connectionState) : Tool
{
    [McpServerTool(Name = "search_work_items", Title = "Search Work Items", ReadOnly = true, Idempotent = true)]
    [Description("Search Azure DevOps work items by free-text query across title and, optionally, description.")]
    public async Task<SearchWorkItemsResponse> ExecuteAsync(
        [Description("Free-text query to search for.")] string query,
        [Description("Maximum number of work items to return.")] int top = 20,
        [Description("Include closed/done/removed work items in the results.")] bool includeClosed = false,
        [Description("Also include matches found only in the description.")] bool includeDescription = false,
        CancellationToken cancellationToken = default)
    {
        if (!connectionState.TryGetRequired(out var connection, out var error))
            return SearchWorkItemsResponse.AsError(error!);

        var result = await client.SearchWorkItemsAsync(
            connection,
            query,
            top,
            includeClosed,
            includeDescription,
            cancellationToken).ConfigureAwait(false);

        return result.Error is not null
            ? SearchWorkItemsResponse.AsError(result.Error)
            : new SearchWorkItemsResponse(result.Results ?? Array.Empty<SearchTicketResult>());
    }
}

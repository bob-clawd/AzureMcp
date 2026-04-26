using AzureMcp.Tools.Configuration;
using AzureMcp.Tools;

namespace AzureMcp.Tools.Clients;

public interface IAzureDevOpsWorkItemClient
{
    Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)> SearchWorkItemsAsync(
        AzureDevOpsConnectionInfo connection,
        string query,
        int top = 20,
        bool includeClosed = false,
        bool includeDescription = false,
        CancellationToken cancellationToken = default);
}

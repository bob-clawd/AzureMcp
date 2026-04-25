using AzureMcp.Tools.Configuration;
using AzureMcp.Tools;

namespace AzureMcp.Tools.WorkItems;

public interface IAzureDevOpsWorkItemClient
{
    Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        CancellationToken cancellationToken = default);
}

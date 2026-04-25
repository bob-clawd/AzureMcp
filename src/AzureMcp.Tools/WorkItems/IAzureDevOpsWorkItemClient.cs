using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.WorkItems;

public interface IAzureDevOpsWorkItemClient
{
    Task<ReadWorkItemResult> ReadWorkItemAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        CancellationToken cancellationToken = default);
}

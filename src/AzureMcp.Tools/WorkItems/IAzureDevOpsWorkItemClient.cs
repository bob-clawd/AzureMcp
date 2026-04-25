namespace AzureMcp.Tools.WorkItems;

public interface IAzureDevOpsWorkItemClient
{
    Task<AzureDevOpsWorkItem> ReadWorkItemAsync(int workItemId, CancellationToken cancellationToken = default);
}

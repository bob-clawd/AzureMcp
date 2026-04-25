namespace AzureMcp.Tools.WorkItems;

public interface IAzureDevOpsWorkItemClient
{
    Task<AzureDevOpsWorkItem> ReadWorkItemAsync(int id, CancellationToken cancellationToken = default);
}

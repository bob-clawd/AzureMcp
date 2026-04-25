namespace AzureMcp.Tools.WorkItems;

public sealed class AzureDevOpsWorkItemNotFoundException(int workItemId)
    : InvalidOperationException($"Azure DevOps work item '{workItemId}' was not found.")
{
    public int WorkItemId { get; } = workItemId;
}

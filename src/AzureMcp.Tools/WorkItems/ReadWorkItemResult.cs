namespace AzureMcp.Tools.WorkItems;

public sealed record ReadWorkItemResult(
    AzureDevOpsWorkItem? WorkItem,
    ErrorInfo? Error = null)
{
    public static ReadWorkItemResult AsError(string message, IReadOnlyDictionary<string, string>? details = null)
        => new(null, new ErrorInfo(message, details));
}

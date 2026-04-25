using System.ComponentModel;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.WorkItems;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools.Tools;

public sealed record WorkItemContextEntry(
    int Id,
    int Level,
    string? Title,
    string? State,
    string? WorkItemType,
    string? DescriptionText,
    string? Url);

public sealed record GetContextResponse(
    int RootWorkItemId,
    IReadOnlyList<WorkItemContextEntry> Items,
    ErrorInfo? Error = null)
{
    public static GetContextResponse AsError(ErrorInfo error)
        => new(0, Array.Empty<WorkItemContextEntry>(), error);
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

        var cache = new Dictionary<int, AzureDevOpsWorkItem>();
        var rootResult = await FindRootAsync(connection, workItemId, cache, cancellationToken).ConfigureAwait(false);
        if (rootResult.Error is not null)
            return GetContextResponse.AsError(rootResult.Error);

        var entries = new List<WorkItemContextEntry>();
        var visited = new HashSet<int>();

        var traversalResult = await TraverseAsync(
            connection,
            rootResult.Root!.Id,
            level: 0,
            cache,
            visited,
            entries,
            cancellationToken).ConfigureAwait(false);

        if (traversalResult is not null)
            return GetContextResponse.AsError(traversalResult);

        return new GetContextResponse(rootResult.Root.Id, entries);
    }

    private async Task<(AzureDevOpsWorkItem? Root, ErrorInfo? Error)> FindRootAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        IDictionary<int, AzureDevOpsWorkItem> cache,
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

            var current = result.WorkItem!;
            if (current.ParentWorkItemId is null)
                return (current, null);

            currentId = current.ParentWorkItemId.Value;
        }
    }

    private async Task<ErrorInfo?> TraverseAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        int level,
        IDictionary<int, AzureDevOpsWorkItem> cache,
        ISet<int> visited,
        ICollection<WorkItemContextEntry> entries,
        CancellationToken cancellationToken)
    {
        if (!visited.Add(workItemId))
            return null;

        var result = await LoadAsync(connection, workItemId, cache, cancellationToken).ConfigureAwait(false);
        if (result.Error is not null)
            return result.Error;

        var workItem = result.WorkItem!;
        entries.Add(new WorkItemContextEntry(
            Id: workItem.Id,
            Level: level,
            Title: workItem.Title,
            State: workItem.State,
            WorkItemType: workItem.WorkItemType,
            DescriptionText: workItem.DescriptionText,
            Url: workItem.Url));

        foreach (var childId in workItem.ChildWorkItemIds)
        {
            var error = await TraverseAsync(connection, childId, level + 1, cache, visited, entries, cancellationToken).ConfigureAwait(false);
            if (error is not null)
                return error;
        }

        return null;
    }

    private async Task<ReadWorkItemResult> LoadAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        IDictionary<int, AzureDevOpsWorkItem> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(workItemId, out var cached))
            return new ReadWorkItemResult(cached);

        var result = await client.ReadWorkItemAsync(connection, workItemId, cancellationToken).ConfigureAwait(false);
        if (result.WorkItem is not null)
            cache[workItemId] = result.WorkItem;

        return result;
    }
}

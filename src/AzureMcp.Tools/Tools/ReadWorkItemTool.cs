using System.ComponentModel;
using AzureMcp.Tools.Clients;
using ModelContextProtocol.Server;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Tools;

public sealed record ReadWorkItemResponse(
    Ticket? Ticket,
    ErrorInfo? Error = null)
{
    public static ReadWorkItemResponse AsError(ErrorInfo error) => new(null, error);
}

public sealed class ReadWorkItemTool(
    IAzureDevOpsWorkItemClient client,
    IAzureDevOpsPullRequestClient pullRequestClient,
    IAzureDevOpsConnectionState connectionState) : Tool
{
    [McpServerTool(Name = "read_work_item", Title = "Read Work Item", ReadOnly = true, Idempotent = true)]
    [Description("Load a single Azure DevOps work item by id and return a structured view with the key fields needed for everyday work.")]
    public async Task<ReadWorkItemResponse> ExecuteAsync(
        [Description("Azure DevOps work item id.")] int workItemId,
        CancellationToken cancellationToken = default)
    {
        if (!connectionState.TryGetRequired(out var connection, out var error))
            return ReadWorkItemResponse.AsError(error!);

        var result = await client.ReadWorkItemAsync(connection, workItemId, cancellationToken).ConfigureAwait(false);
        if (result.Error is not null)
            return ReadWorkItemResponse.AsError(result.Error);

        var ticket = result.Ticket!;

        if (ticket.PullRequestRefs.Count == 0)
            return new ReadWorkItemResponse(ticket);

        const int maxPullRequestsToLoad = 10;
        var pullRequests = new List<PullRequestInfo>();

        foreach (var prRef in ticket.PullRequestRefs
                     .OrderBy(static x => x.PullRequestId)
                     .DistinctBy(static x => x.PullRequestId)
                     .Take(maxPullRequestsToLoad))
        {
            var pr = await pullRequestClient.ReadPullRequestAsync(
                connection,
                prRef.ProjectId,
                prRef.RepositoryId,
                prRef.PullRequestId,
                cancellationToken).ConfigureAwait(false);

            if (pr.PullRequest is not null)
                pullRequests.Add(pr.PullRequest);
        }

        if (pullRequests.Count == 0)
            return new ReadWorkItemResponse(ticket);

        ticket = ticket with { PullRequests = pullRequests };
        return new ReadWorkItemResponse(ticket);
    }
}

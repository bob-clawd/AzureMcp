using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Clients;

public interface IAzureDevOpsPullRequestClient
{
    Task<(PullRequestInfo? PullRequest, ErrorInfo? Error)> ReadPullRequestAsync(
        AzureDevOpsConnectionInfo connection,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default);
}

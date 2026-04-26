using AzureMcp.Tools.Tools;
using AzureMcp.Tools.WorkItems;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools;

namespace AzureMcp.Tools.Tests.Tools;

public sealed class ReadWorkItemToolTests
{
    [Fact]
    public async Task ExecuteAsync_MapsClientResultToToolResponse()
    {
        var pullRequests = new SpyPullRequestClient();
        var tool = new ReadWorkItemTool(new FakeClient(), pullRequests, new ConfiguredState());

        var result = await tool.ExecuteAsync(42);

        result.Error.IsNull();
        result.Ticket.IsNotNull();
        result.Ticket!.Id.Is(42);
        result.Ticket.Title.Is("Investigate flaky deployment");
        result.Ticket.State.Is("New");
        result.Ticket.WorkItemType.Is("Bug");
        result.Ticket.AssignedTo.Is("Grace Hopper");
        pullRequests.Calls.Is(0);
    }

    [Fact]
    public async Task ExecuteAsync_LoadsPullRequestDescriptions_WhenLinkedFromWorkItem()
    {
        var pullRequests = new FakePullRequestClient();
        var tool = new ReadWorkItemTool(new FakeClientWithPullRequests(), pullRequests, new ConfiguredState());

        var result = await tool.ExecuteAsync(42);

        result.Error.IsNull();
        result.Ticket.IsNotNull();
        result.Ticket!.PullRequests.Select(pr => pr.Id).Is(33);
        result.Ticket.PullRequests.Select(pr => pr.DescriptionText).Is("PR description text");
        pullRequests.Calls.Is(1);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNotConfigured_WithoutCallingClient()
    {
        var client = new SpyClient();
        var pullRequests = new SpyPullRequestClient();
        var tool = new ReadWorkItemTool(client, pullRequests, new MissingState());

        var result = await tool.ExecuteAsync(123);

        result.Error.IsNotNull();
        result.Ticket.IsNull();
        result.Error!.Message.IsContaining("Missing:");
        result.Error!.Message.IsContaining("organizationUrl");
        result.Error!.Message.IsContaining("personalAccessToken");
        result.Error!.Message.IsContaining("config file");
        client.Calls.Is(0);
        pullRequests.Calls.Is(0);
    }

    private sealed class FakeClient : IAzureDevOpsWorkItemClient
    {
        public Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
        {
            var ticket = new Ticket
            {
                Id = workItemId,
                Title = "Investigate flaky deployment",
                State = "New",
                WorkItemType = "Bug",
                DescriptionText = "Look at the failed release logs.",
                AssignedTo = "Grace Hopper",
                ParentId = 1,
                ChildrenIds = new[] { 2, 3 },
                Branches = Array.Empty<string>(),
                PullRequests = Array.Empty<PullRequestInfo>()
            };

            return Task.FromResult<(Ticket? Ticket, ErrorInfo? Error)>((ticket, null));
        }

        public Task<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)> SearchWorkItemsAsync(AzureDevOpsConnectionInfo connection, string query, int top = 20, bool includeClosed = false, bool includeDescription = false, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SearchWorkItemsAsync should not be called in this test.");
    }

    private sealed class SpyClient : IAzureDevOpsWorkItemClient
    {
        public int Calls { get; private set; }

        public Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Client should not be called when configuration is missing.");
        }

        public Task<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)> SearchWorkItemsAsync(AzureDevOpsConnectionInfo connection, string query, int top = 20, bool includeClosed = false, bool includeDescription = false, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SearchWorkItemsAsync should not be called in this test.");
    }

    private sealed class FakeClientWithPullRequests : IAzureDevOpsWorkItemClient
    {
        public Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
        {
            var ticket = new Ticket
            {
                Id = workItemId,
                Title = "Investigate linked PR",
                State = "Active",
                WorkItemType = "Bug",
                DescriptionText = "See linked PR.",
                AssignedTo = null,
                ParentId = null,
                ChildrenIds = Array.Empty<int>(),
                Branches = Array.Empty<string>(),
                PullRequestRefs = new[]
                {
                    new PullRequestRef(
                        "11111111-1111-1111-1111-111111111111",
                        "22222222-2222-2222-2222-222222222222",
                        33)
                }
            };

            return Task.FromResult<(Ticket? Ticket, ErrorInfo? Error)>((ticket, null));
        }

        public Task<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)> SearchWorkItemsAsync(AzureDevOpsConnectionInfo connection, string query, int top = 20, bool includeClosed = false, bool includeDescription = false, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SearchWorkItemsAsync should not be called in this test.");
    }

    private sealed class FakePullRequestClient : AzureMcp.Tools.Git.IAzureDevOpsPullRequestClient
    {
        public int Calls { get; private set; }

        public Task<(PullRequestInfo? PullRequest, ErrorInfo? Error)> ReadPullRequestAsync(AzureDevOpsConnectionInfo connection, string projectId, string repositoryId, int pullRequestId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<(PullRequestInfo? PullRequest, ErrorInfo? Error)>((new PullRequestInfo(pullRequestId, "PR description text"), null));
        }
    }

    private sealed class SpyPullRequestClient : AzureMcp.Tools.Git.IAzureDevOpsPullRequestClient
    {
        public int Calls { get; private set; }

        public Task<(PullRequestInfo? PullRequest, ErrorInfo? Error)> ReadPullRequestAsync(AzureDevOpsConnectionInfo connection, string projectId, string repositoryId, int pullRequestId, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Pull request client should not be called for this test.");
        }
    }

    private sealed class ConfiguredState : IAzureDevOpsConnectionState
    {
        public bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out ErrorInfo? error)
        {
            connection = new AzureDevOpsConnectionInfo("https://dev.azure.com/test-org", "pat", null);
            error = null;
            return true;
        }

        public string ConfigPath => "/tmp/azuremcp.json";
    }

    private sealed class MissingState : IAzureDevOpsConnectionState
    {
        public bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out ErrorInfo? error)
        {
            connection = default!;
            error = AzureMcpErrors.MissingConfig(ConfigPath, ["organizationUrl", "personalAccessToken"]);
            return false;
        }

        public string ConfigPath => "/tmp/azuremcp.json";
    }
}

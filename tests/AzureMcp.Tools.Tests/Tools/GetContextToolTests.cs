using AzureMcp.Tools;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.Tools;
using AzureMcp.Tools.WorkItems;

namespace AzureMcp.Tools.Tests.Tools;

public sealed class GetContextToolTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCalledWithChild_ReturnsParentChainContext()
    {
        var tool = new GetContextTool(new FakeClient(), new ConfiguredState());

        var result = await tool.ExecuteAsync(3);

        result.Error.IsNull();
        result.Tickets.Select(item => item.Id).Is(1, 2, 3);
        result.Tickets.Select(item => item.DescriptionText).Is(
            "Root description",
            "Child description",
            "Grandchild description");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalledWithRoot_ReturnsOnlyRoot()
    {
        var tool = new GetContextTool(new FakeClient(), new ConfiguredState());

        var result = await tool.ExecuteAsync(1);

        result.Error.IsNull();
        result.Tickets.Select(item => item.Id).Is(1);
        result.Tickets.Select(item => item.DescriptionText).Is("Root description");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalledWithSibling_ReturnsRootToSiblingChain()
    {
        var tool = new GetContextTool(new FakeClient(), new ConfiguredState());

        var result = await tool.ExecuteAsync(4);

        result.Error.IsNull();
        result.Tickets.Select(item => item.Id).Is(1, 4);
        result.Tickets.Select(item => item.DescriptionText).Is("Root description", "Sibling description");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConfigError_WhenNotConfigured_WithoutCallingClient()
    {
        var client = new SpyClient();
        var tool = new GetContextTool(client, new MissingState());

        var result = await tool.ExecuteAsync(3);

        result.Error.IsNotNull();
        result.Error!.Message.IsContaining("config file");
        client.Calls.Is(0);
    }

    private sealed class FakeClient : IAzureDevOpsWorkItemClient
    {
        private readonly IReadOnlyDictionary<int, Ticket> _items = new Dictionary<int, Ticket>
        {
            [1] = new(
                Id: 1,
                Title: "Root",
                State: "Active",
                WorkItemType: "Feature",
                DescriptionText: "Root description",
                AssignedTo: null,
                ParentId: null,
                ChildrenIds: [2, 4],
                Branches: Array.Empty<string>(),
                PullRequestIds: Array.Empty<int>()),
            [2] = new(
                Id: 2,
                Title: "Child",
                State: "Active",
                WorkItemType: "Bug",
                DescriptionText: "Child description",
                AssignedTo: null,
                ParentId: 1,
                ChildrenIds: [3],
                Branches: Array.Empty<string>(),
                PullRequestIds: Array.Empty<int>()),
            [3] = new(
                Id: 3,
                Title: "Grandchild",
                State: "New",
                WorkItemType: "Task",
                DescriptionText: "Grandchild description",
                AssignedTo: null,
                ParentId: 2,
                ChildrenIds: [],
                Branches: Array.Empty<string>(),
                PullRequestIds: Array.Empty<int>()),
            [4] = new(
                Id: 4,
                Title: "Sibling",
                State: "New",
                WorkItemType: "Task",
                DescriptionText: "Sibling description",
                AssignedTo: null,
                ParentId: 1,
                ChildrenIds: [],
                Branches: Array.Empty<string>(),
                PullRequestIds: Array.Empty<int>())
        };

        public Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<(Ticket? Ticket, ErrorInfo? Error)>(_items.TryGetValue(workItemId, out var item)
                ? (item, null)
                : (null, new ErrorInfo("work item not found")));
    }

    private sealed class SpyClient : IAzureDevOpsWorkItemClient
    {
        public int Calls { get; private set; }

        public Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Client should not be called when configuration is missing.");
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

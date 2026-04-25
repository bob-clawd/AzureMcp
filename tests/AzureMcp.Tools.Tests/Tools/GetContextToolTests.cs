using AzureMcp.Tools;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.Tools;
using AzureMcp.Tools.WorkItems;

namespace AzureMcp.Tools.Tests.Tools;

public sealed class GetContextToolTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCalledWithChild_ReturnsRootToChildrenContext()
    {
        var tool = new GetContextTool(new FakeClient(), new ConfiguredState());

        var result = await tool.ExecuteAsync(3);

        result.Error.IsNull();
        result.Tickets.Select(item => item.Id).Is(1, 2, 3, 4);
        result.Tickets.Select(item => item.DescriptionText).Is(
            "Root description",
            "Child description",
            "Grandchild description",
            "Sibling description");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSameContext_ForRootAndNestedChild()
    {
        var tool = new GetContextTool(new FakeClient(), new ConfiguredState());

        var rootResult = await tool.ExecuteAsync(1);
        var childResult = await tool.ExecuteAsync(3);

        rootResult.Error.IsNull();
        childResult.Error.IsNull();
        rootResult.Tickets.Select(item => item.Id).Is(childResult.Tickets.Select(item => item.Id).ToArray());
        rootResult.Tickets.Select(item => item.DescriptionText).Is(childResult.Tickets.Select(item => item.DescriptionText).ToArray());
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
                ParentTicketId: null,
                ChildTicketIds: [2, 4]),
            [2] = new(
                Id: 2,
                Title: "Child",
                State: "Active",
                WorkItemType: "Bug",
                DescriptionText: "Child description",
                AssignedTo: null,
                ParentTicketId: 1,
                ChildTicketIds: [3]),
            [3] = new(
                Id: 3,
                Title: "Grandchild",
                State: "New",
                WorkItemType: "Task",
                DescriptionText: "Grandchild description",
                AssignedTo: null,
                ParentTicketId: 2,
                ChildTicketIds: []),
            [4] = new(
                Id: 4,
                Title: "Sibling",
                State: "New",
                WorkItemType: "Task",
                DescriptionText: "Sibling description",
                AssignedTo: null,
                ParentTicketId: 1,
                ChildTicketIds: [])
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

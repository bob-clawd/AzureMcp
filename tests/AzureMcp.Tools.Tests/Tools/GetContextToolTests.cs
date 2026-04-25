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
        result.RootWorkItemId.Is(1);
        result.Items.Select(item => item.Id).Is(1, 2, 3, 4);
        result.Items.Select(item => item.Level).Is(0, 1, 2, 1);
        result.Items.Select(item => item.DescriptionText).Is(
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
        rootResult.RootWorkItemId.Is(childResult.RootWorkItemId);
        rootResult.Items.Select(item => item.Id).Is(childResult.Items.Select(item => item.Id).ToArray());
        rootResult.Items.Select(item => item.DescriptionText).Is(childResult.Items.Select(item => item.DescriptionText).ToArray());
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
        private readonly IReadOnlyDictionary<int, AzureDevOpsWorkItem> _items = new Dictionary<int, AzureDevOpsWorkItem>
        {
            [1] = new(
                Id: 1,
                Title: "Root",
                State: "Active",
                WorkItemType: "Feature",
                DescriptionText: "Root description",
                DescriptionHtml: null,
                AssignedTo: null,
                ParentWorkItemId: null,
                ChildWorkItemIds: [2, 4],
                RelatedWorkItemIds: [],
                Url: "https://dev.azure.com/test-org/_apis/wit/workItems/1"),
            [2] = new(
                Id: 2,
                Title: "Child",
                State: "Active",
                WorkItemType: "Bug",
                DescriptionText: "Child description",
                DescriptionHtml: null,
                AssignedTo: null,
                ParentWorkItemId: 1,
                ChildWorkItemIds: [3],
                RelatedWorkItemIds: [],
                Url: "https://dev.azure.com/test-org/_apis/wit/workItems/2"),
            [3] = new(
                Id: 3,
                Title: "Grandchild",
                State: "New",
                WorkItemType: "Task",
                DescriptionText: "Grandchild description",
                DescriptionHtml: null,
                AssignedTo: null,
                ParentWorkItemId: 2,
                ChildWorkItemIds: [],
                RelatedWorkItemIds: [],
                Url: "https://dev.azure.com/test-org/_apis/wit/workItems/3"),
            [4] = new(
                Id: 4,
                Title: "Sibling",
                State: "New",
                WorkItemType: "Task",
                DescriptionText: "Sibling description",
                DescriptionHtml: null,
                AssignedTo: null,
                ParentWorkItemId: 1,
                ChildWorkItemIds: [],
                RelatedWorkItemIds: [],
                Url: "https://dev.azure.com/test-org/_apis/wit/workItems/4")
        };

        public Task<ReadWorkItemResult> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.TryGetValue(workItemId, out var item)
                ? new ReadWorkItemResult(item)
                : ReadWorkItemResult.AsError("work item not found"));
    }

    private sealed class SpyClient : IAzureDevOpsWorkItemClient
    {
        public int Calls { get; private set; }

        public Task<ReadWorkItemResult> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
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

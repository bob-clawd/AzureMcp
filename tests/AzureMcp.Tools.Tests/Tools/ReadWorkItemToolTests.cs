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
        var tool = new ReadWorkItemTool(new FakeClient(), new ConfiguredState());

        var result = await tool.ExecuteAsync(42);

        result.Id.Is(42);
        result.Title.Is("Investigate flaky deployment");
        result.State.Is("New");
        result.WorkItemType.Is("Bug");
        result.AssignedTo?.DisplayName.Is("Grace Hopper");
        result.Error.IsNull();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNotConfigured_WithoutCallingClient()
    {
        var client = new SpyClient();
        var tool = new ReadWorkItemTool(client, new MissingState());

        var result = await tool.ExecuteAsync(123);

        result.Error.IsNotNull();
        result.Error!.Message.IsContaining("Missing:");
        result.Error!.Message.IsContaining("organizationUrl");
        result.Error!.Message.IsContaining("personalAccessToken");
        result.Error!.Message.IsContaining("config file");
        client.Calls.Is(0);
    }

    private sealed class FakeClient : IAzureDevOpsWorkItemClient
    {
        public Task<ReadWorkItemResult> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
        {
            var workItem = new AzureDevOpsWorkItem(
                Id: workItemId,
                Title: "Investigate flaky deployment",
                State: "New",
                WorkItemType: "Bug",
                DescriptionText: "Look at the failed release logs.",
                DescriptionHtml: "<div>Look at the failed release logs.</div>",
                AssignedTo: new AzureDevOpsAssignedTo("Grace Hopper", "grace@example.com"),
                ParentWorkItemId: 1,
                ChildWorkItemIds: new[] { 2, 3 },
                RelatedWorkItemIds: new[] { 99 },
                Url: $"https://dev.azure.com/test-org/_apis/wit/workItems/{workItemId}");

            return Task.FromResult(new ReadWorkItemResult(workItem));
        }
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

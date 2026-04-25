using AzureMcp.Tools.Tools;
using AzureMcp.Tools.WorkItems;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Tests.Tools;

public sealed class ReadWorkItemToolTests
{
    [Fact]
    public async Task ExecuteAsync_MapsClientResultToToolResponse()
    {
        var tool = new ReadWorkItemTool(new FakeClient());

        var result = await tool.ExecuteAsync(42);

        Assert.Equal(42, result.Id);
        Assert.Equal("Investigate flaky deployment", result.Title);
        Assert.Equal("New", result.State);
        Assert.Equal("Bug", result.WorkItemType);
        Assert.Equal("Grace Hopper", result.AssignedTo?.DisplayName);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsErrorString_WhenNotConfigured()
    {
        var tool = new ReadWorkItemTool(new UnconfiguredClient());

        var result = await tool.ExecuteAsync(123);

        Assert.NotNull(result.Error);
        Assert.Contains("Missing:", result.Error);
        Assert.Contains("AZURE_MCP_PAT", result.Error);
        Assert.Contains("AZURE_MCP_ORGANIZATION_URL", result.Error);
        Assert.Contains("configure_connection", result.Error);
    }

    private sealed class FakeClient : IAzureDevOpsWorkItemClient
    {
        public Task<AzureDevOpsWorkItem> ReadWorkItemAsync(int workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AzureDevOpsWorkItem(
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
                Url: $"https://dev.azure.com/test-org/_apis/wit/workItems/{workItemId}"));
    }

    private sealed class UnconfiguredClient : IAzureDevOpsWorkItemClient
    {
        public Task<AzureDevOpsWorkItem> ReadWorkItemAsync(int workItemId, CancellationToken cancellationToken = default)
            => throw new AzureMcpConfigurationException([
                "AZURE_MCP_ORGANIZATION_URL",
                "AZURE_MCP_PAT"
            ]);
    }
}

using AzureMcp.Tools.Tools;
using AzureMcp.Tools.WorkItems;

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
                Url: $"https://dev.azure.com/test-org/_apis/wit/workItems/{workItemId}"));
    }
}

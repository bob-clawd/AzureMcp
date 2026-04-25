using AzureMcp.Tools.Tools;
using AzureMcp.Tools.WorkItems;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Tests.Tools;

public sealed class ReadWorkItemToolTests
{
    [Fact]
    public async Task ExecuteAsync_MapsClientResultToToolResponse()
    {
        var tool = new ReadWorkItemTool(new FakeClient(), new ConfiguredState());

        var result = await tool.ExecuteAsync(42);

        Assert.Equal(42, result.Id);
        Assert.Equal("Investigate flaky deployment", result.Title);
        Assert.Equal("New", result.State);
        Assert.Equal("Bug", result.WorkItemType);
        Assert.Equal("Grace Hopper", result.AssignedTo?.DisplayName);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNotConfigured_WithoutCallingClient()
    {
        var client = new SpyClient();
        var tool = new ReadWorkItemTool(client, new MissingState());

        var result = await tool.ExecuteAsync(123);

        Assert.NotNull(result.Error);
        Assert.Contains("Missing:", result.Error!.Message);
        Assert.Contains("AZURE_MCP_PAT", result.Error!.Message);
        Assert.Contains("AZURE_MCP_ORGANIZATION_URL", result.Error!.Message);
        Assert.Contains("configure_connection", result.Error!.Message);
        Assert.Equal(new[] { "AZURE_MCP_ORGANIZATION_URL", "AZURE_MCP_PAT" }, result.MissingEnvironmentVariables);
        Assert.Equal(0, client.Calls);
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
        public AzureDevOpsConnectionInfo GetRequired() => new("https://dev.azure.com/test-org", "pat", null);

        public bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out IReadOnlyList<string> missingEnvironmentVariables)
        {
            connection = GetRequired();
            missingEnvironmentVariables = Array.Empty<string>();
            return true;
        }

        public void Set(string? organizationUrl, string? personalAccessToken, string? project) { }
    }

    private sealed class MissingState : IAzureDevOpsConnectionState
    {
        public AzureDevOpsConnectionInfo GetRequired() => throw new InvalidOperationException();

        public bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out IReadOnlyList<string> missingEnvironmentVariables)
        {
            connection = default!;
            missingEnvironmentVariables = new[] { "AZURE_MCP_ORGANIZATION_URL", "AZURE_MCP_PAT" };
            return false;
        }

        public void Set(string? organizationUrl, string? personalAccessToken, string? project) { }
    }
}

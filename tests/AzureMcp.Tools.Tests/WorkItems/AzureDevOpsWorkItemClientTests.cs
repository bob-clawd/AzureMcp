using System.Net;
using System.Text;
using AzureMcp.Tools.WorkItems;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Tests.WorkItems;

public sealed class AzureDevOpsWorkItemClientTests
{
    [Fact]
    public async Task ReadWorkItemAsync_ParsesStructuredResponse()
    {
        const string payload = """
        {
          "id": 12345,
          "url": "https://dev.azure.com/test-org/_apis/wit/workItems/12345",
          "relations": [
            {
              "rel": "System.LinkTypes.Hierarchy-Reverse",
              "url": "https://dev.azure.com/test-org/_apis/wit/workItems/100"
            },
            {
              "rel": "System.LinkTypes.Hierarchy-Forward",
              "url": "https://dev.azure.com/test-org/_apis/wit/workItems/200"
            },
            {
              "rel": "System.LinkTypes.Hierarchy-Forward",
              "url": "https://dev.azure.com/test-org/_apis/wit/workItems/201"
            },
            {
              "rel": "System.LinkTypes.Related",
              "url": "https://dev.azure.com/test-org/_apis/wit/workItems/300"
            }
          ],
          "fields": {
            "System.Title": "Improve deployment diagnostics",
            "System.State": "Active",
            "System.WorkItemType": "User Story",
            "System.Description": "<div><p>Investigate missing logs during deployment.</p><p>Check retention.</p></div>",
            "System.AssignedTo": {
              "displayName": "Ada Lovelace",
              "uniqueName": "ada@example.com"
            }
          }
        }
        """;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }))
        ;

        var client = new AzureDevOpsWorkItemClient(httpClient);
        var connection = new AzureDevOpsConnectionInfo("https://dev.azure.com/test-org", "secret-pat", null);
        var result = await client.ReadWorkItemAsync(connection, 12345);

        Assert.Null(result.Error);
        var workItem = result.WorkItem!;

        Assert.Equal(12345, workItem.Id);
        Assert.Equal("Improve deployment diagnostics", workItem.Title);
        Assert.Equal("Active", workItem.State);
        Assert.Equal("User Story", workItem.WorkItemType);
        Assert.Equal("Ada Lovelace", workItem.AssignedTo?.DisplayName);
        Assert.Equal("ada@example.com", workItem.AssignedTo?.UniqueName);
        Assert.Equal("Investigate missing logs during deployment.\n\nCheck retention.", workItem.DescriptionText);
        Assert.Equal(100, workItem.ParentWorkItemId);
        Assert.Equal(new[] { 200, 201 }, workItem.ChildWorkItemIds);
        Assert.Equal(new[] { 300 }, workItem.RelatedWorkItemIds);
    }

    [Fact]
    public async Task ReadWorkItemAsync_ThrowsSpecificException_WhenNotFound()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var client = new AzureDevOpsWorkItemClient(httpClient);
        var connection = new AzureDevOpsConnectionInfo("https://dev.azure.com/test-org", "secret-pat", null);

        var result = await client.ReadWorkItemAsync(connection, 404);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}

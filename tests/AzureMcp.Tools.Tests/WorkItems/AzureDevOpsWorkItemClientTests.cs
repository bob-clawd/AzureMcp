using System.Net;
using System.Text;
using AzureMcp.Tools.WorkItems;

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
        {
            BaseAddress = new Uri("https://dev.azure.com/test-org/")
        };

        var client = new AzureDevOpsWorkItemClient(httpClient);
        var workItem = await client.ReadWorkItemAsync(12345);

        Assert.Equal(12345, workItem.Id);
        Assert.Equal("Improve deployment diagnostics", workItem.Title);
        Assert.Equal("Active", workItem.State);
        Assert.Equal("User Story", workItem.WorkItemType);
        Assert.Equal("Ada Lovelace", workItem.AssignedTo?.DisplayName);
        Assert.Equal("ada@example.com", workItem.AssignedTo?.UniqueName);
        Assert.Equal("Investigate missing logs during deployment.\n\nCheck retention.", workItem.DescriptionText);
    }

    [Fact]
    public async Task ReadWorkItemAsync_ThrowsSpecificException_WhenNotFound()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)))
        {
            BaseAddress = new Uri("https://dev.azure.com/test-org/")
        };

        var client = new AzureDevOpsWorkItemClient(httpClient);

        await Assert.ThrowsAsync<AzureDevOpsWorkItemNotFoundException>(() => client.ReadWorkItemAsync(404));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}

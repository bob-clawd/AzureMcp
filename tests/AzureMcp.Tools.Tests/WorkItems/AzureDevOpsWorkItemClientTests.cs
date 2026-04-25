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

        result.Error.IsNull();
        var ticket = result.Ticket!;

        ticket.Id.Is(12345);
        ticket.Title.Is("Improve deployment diagnostics");
        ticket.State.Is("Active");
        ticket.WorkItemType.Is("User Story");
        ticket.AssignedTo.Is("Ada Lovelace");
        ticket.DescriptionText.Is("Investigate missing logs during deployment.\n\nCheck retention.");
        ticket.ParentTicketId.Is(100);
        ticket.ChildTicketIds.Is([200, 201]);
    }

    [Fact]
    public async Task ReadWorkItemAsync_ThrowsSpecificException_WhenNotFound()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var client = new AzureDevOpsWorkItemClient(httpClient);
        var connection = new AzureDevOpsConnectionInfo("https://dev.azure.com/test-org", "secret-pat", null);

        var result = await client.ReadWorkItemAsync(connection, 404);
        result.Error.IsNotNull();
        result.Error!.Message.ToLowerInvariant().IsContaining("not found");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}

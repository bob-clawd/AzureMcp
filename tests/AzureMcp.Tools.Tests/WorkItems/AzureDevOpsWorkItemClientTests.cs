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
            },
            {
              "rel": "ArtifactLink",
              "url": "vstfs:///Git/Ref/11111111-1111-1111-1111-111111111111%2F22222222-2222-2222-2222-222222222222%2Frefs%2Fheads%2Ffeature%2Fado-12345",
              "attributes": {
                "name": "Branch"
              }
            },
            {
              "rel": "ArtifactLink",
              "url": "vstfs:///Git/PullRequestId/11111111-1111-1111-1111-111111111111%2F22222222-2222-2222-2222-222222222222%2F33",
              "attributes": {
                "name": "Pull Request"
              }
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
        ticket.ParentId.Is(100);
        ticket.ChildrenIds.Is([200, 201]);
        ticket.Branches.Is(["feature/ado-12345"]);
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

    [Fact]
    public async Task SearchWorkItemsAsync_UsesSearchEndpoint_AndFiltersToOpenTitleMatchesByDefault()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        const string payload = """
        {
          "count": 3,
          "results": [
            {
              "fields": {
                "system.id": "11",
                "system.title": "Deploy pipeline fails",
                "system.state": "Active",
                "system.workitemtype": "Bug",
                "system.changeddate": "2026-04-26T07:00:00Z"
              },
              "hits": [
                {
                  "fieldReferenceName": "system.title",
                  "highlights": ["<highlighthit>Deploy</highlighthit> pipeline fails"]
                }
              ]
            },
            {
              "fields": {
                "system.id": "12",
                "system.title": "Investigate flaky tests",
                "system.state": "Active",
                "system.workitemtype": "Task",
                "system.changeddate": "2026-04-26T06:00:00Z"
              },
              "hits": [
                {
                  "fieldReferenceName": "system.description",
                  "highlights": ["fix <highlighthit>deploy</highlighthit> notes"]
                }
              ]
            },
            {
              "fields": {
                "system.id": "13",
                "system.title": "Deploy docs cleanup",
                "system.state": "Closed",
                "system.workitemtype": "Task",
                "system.changeddate": "2026-04-26T08:00:00Z"
              },
              "hits": [
                {
                  "fieldReferenceName": "system.title",
                  "highlights": ["<highlighthit>Deploy</highlighthit> docs cleanup"]
                }
              ]
            }
          ]
        }
        """;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }));

        var client = new AzureDevOpsWorkItemClient(httpClient);
        var connection = new AzureDevOpsConnectionInfo("https://dev.azure.com/test-org", "secret-pat", "Demo Project");

        var result = await client.SearchWorkItemsAsync(connection, "deploy", top: 20, includeClosed: false, includeDescription: false);

        result.Error.IsNull();
        result.Results!.Select(ticket => ticket.Id).Is(11);
        capturedRequest.IsNotNull();
        capturedBody.IsNotNull();
        var body = capturedBody!;
        capturedRequest!.Method.Is(HttpMethod.Post);
        capturedRequest.RequestUri!.AbsoluteUri.Is("https://almsearch.dev.azure.com/test-org/Demo%20Project/_apis/search/workitemsearchresults?api-version=7.1");
        body.IsContaining("\"searchText\":\"deploy\"");
        body.IsContaining("\"$top\":60");
    }

    [Fact]
    public async Task SearchWorkItemsAsync_WhenIncludeDescriptionTrue_IncludesDescriptionMatches_AndKeepsTitleHitsFirst()
    {
        const string payload = """
        {
          "count": 2,
          "results": [
            {
              "fields": {
                "system.id": "21",
                "system.title": "Deploy pipeline fails",
                "system.state": "Active",
                "system.workitemtype": "Bug",
                "system.changeddate": "2026-04-26T07:00:00Z"
              },
              "hits": [
                {
                  "fieldReferenceName": "system.title",
                  "highlights": ["<highlighthit>Deploy</highlighthit> pipeline fails"]
                }
              ]
            },
            {
              "fields": {
                "system.id": "22",
                "system.title": "Investigate flaky tests",
                "system.state": "New",
                "system.workitemtype": "Task",
                "system.changeddate": "2026-04-26T08:00:00Z"
              },
              "hits": [
                {
                  "fieldReferenceName": "system.description",
                  "highlights": ["fix <highlighthit>deploy</highlighthit> notes"]
                }
              ]
            }
          ]
        }
        """;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }));

        var client = new AzureDevOpsWorkItemClient(httpClient);
        var connection = new AzureDevOpsConnectionInfo("https://dev.azure.com/test-org", "secret-pat", null);

        var result = await client.SearchWorkItemsAsync(connection, "deploy", includeDescription: true);

        result.Error.IsNull();
        result.Results!.Select(ticket => ticket.Id).Is(21, 22);
        result.Results!.Select(ticket => ticket.ChangedDate).Is("2026-04-26T07:00:00Z", "2026-04-26T08:00:00Z");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}

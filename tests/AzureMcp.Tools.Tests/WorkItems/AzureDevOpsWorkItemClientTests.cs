using System.Net;
using System.Text;
using AzureMcp.Tools.Clients;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.Tests.Fixtures;

namespace AzureMcp.Tools.Tests.WorkItems;

public sealed class AzureDevOpsWorkItemClientTests
{
    [Fact]
    public async Task ReadWorkItemAsync_ParsesStructuredResponse()
    {
        var payload = FixtureCatalog.ReadText("ApiResponses", "work-item-12345.json");

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }));

        var client = new AzureDevOpsWorkItemClient(new StubRequestDispatcher(httpClient));
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

        var client = new AzureDevOpsWorkItemClient(new StubRequestDispatcher(httpClient));
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
        var payload = FixtureCatalog.ReadText("ApiResponses", "search-work-items-deploy-default.json");

        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }));

        var client = new AzureDevOpsWorkItemClient(new StubRequestDispatcher(httpClient));
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
        var payload = FixtureCatalog.ReadText("ApiResponses", "search-work-items-deploy-with-description.json");

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }));

        var client = new AzureDevOpsWorkItemClient(new StubRequestDispatcher(httpClient));
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

    private sealed class StubRequestDispatcher(HttpClient httpClient) : IAzureDevOpsRequestDispatcher
    {
        public Task<HttpResponseMessage> SendAsync(
            AzureDevOpsConnectionInfo connection,
            Func<HttpRequestMessage> requestFactory,
            CancellationToken cancellationToken = default)
            => httpClient.SendAsync(requestFactory(), cancellationToken);
    }
}

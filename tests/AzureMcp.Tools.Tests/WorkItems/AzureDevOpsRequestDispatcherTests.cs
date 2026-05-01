using System.Net;
using AzureMcp.Tools.Clients;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Tests.WorkItems;

public sealed class AzureDevOpsRequestDispatcherTests
{
    [Fact]
    public async Task SendAsync_UsesWindowsOnly_WhenPatMissing()
    {
        var patHandler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var windowsHandler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var dispatcher = CreateDispatcher(patHandler, windowsHandler);
        var connection = new AzureDevOpsConnectionInfo("https://ado.contoso.local/tfs/DefaultCollection", null, null);

        using var response = await dispatcher.SendAsync(connection, () => new HttpRequestMessage(HttpMethod.Get, connection.OrganizationUrl));

        response.StatusCode.Is(HttpStatusCode.OK);
        patHandler.Calls.Is(0);
        windowsHandler.Calls.Is(1);
        windowsHandler.Requests.Single().Headers.Authorization.IsNull();
    }

    [Fact]
    public async Task SendAsync_UsesPatWithoutFallback_WhenPatSucceeds()
    {
        var patHandler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var windowsHandler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var dispatcher = CreateDispatcher(patHandler, windowsHandler);
        var connection = new AzureDevOpsConnectionInfo("https://ado.contoso.local/tfs/DefaultCollection", "secret", null);

        using var response = await dispatcher.SendAsync(connection, () => new HttpRequestMessage(HttpMethod.Get, connection.OrganizationUrl));

        response.StatusCode.Is(HttpStatusCode.OK);
        patHandler.Calls.Is(1);
        windowsHandler.Calls.Is(0);
        patHandler.Requests.Single().Headers.Authorization!.Scheme.Is("Basic");
    }

    [Fact]
    public async Task SendAsync_FallsBackToWindows_AndSticksAfterPatAuthFailure()
    {
        var patHandler = new SequenceHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var windowsHandler = new SequenceHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK),
            _ => new HttpResponseMessage(HttpStatusCode.OK));
        var dispatcher = CreateDispatcher(patHandler, windowsHandler);
        var connection = new AzureDevOpsConnectionInfo("https://ado.contoso.local/tfs/DefaultCollection", "secret", null);

        using var first = await dispatcher.SendAsync(connection, () => new HttpRequestMessage(HttpMethod.Get, connection.OrganizationUrl));
        using var second = await dispatcher.SendAsync(connection, () => new HttpRequestMessage(HttpMethod.Get, connection.OrganizationUrl + "/_apis/test"));

        first.StatusCode.Is(HttpStatusCode.OK);
        second.StatusCode.Is(HttpStatusCode.OK);
        patHandler.Calls.Is(1);
        windowsHandler.Calls.Is(2);
    }

    [Fact]
    public async Task SendAsync_ReturnsWindowsAuthFailure_WhenFallbackAlsoFails()
    {
        var patHandler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var windowsHandler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var dispatcher = CreateDispatcher(patHandler, windowsHandler);
        var connection = new AzureDevOpsConnectionInfo("https://ado.contoso.local/tfs/DefaultCollection", "secret", null);

        using var response = await dispatcher.SendAsync(connection, () => new HttpRequestMessage(HttpMethod.Get, connection.OrganizationUrl));

        response.StatusCode.Is(HttpStatusCode.Unauthorized);
        patHandler.Calls.Is(1);
        windowsHandler.Calls.Is(1);
    }

    private static AzureDevOpsRequestDispatcher CreateDispatcher(SequenceHandler patHandler, SequenceHandler windowsHandler)
        => new(
            new StubHttpClientFactory(
                new HttpClient(patHandler),
                new HttpClient(windowsHandler)),
            new AzureDevOpsAuthState());

    private sealed class StubHttpClientFactory(HttpClient patClient, HttpClient windowsClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => name switch
        {
            AzureDevOpsRequestDispatcher.PatClientName => patClient,
            AzureDevOpsRequestDispatcher.WindowsClientName => windowsClient,
            _ => throw new InvalidOperationException($"Unknown client '{name}'.")
        };
    }

    private sealed class SequenceHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responders) : HttpMessageHandler
    {
        private int callIndex;

        public int Calls => callIndex;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));
            var currentIndex = callIndex++;
            var responder = responders[Math.Min(currentIndex, responders.Length - 1)];
            return Task.FromResult(responder(request));
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return clone;
        }
    }
}

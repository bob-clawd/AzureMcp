using AzureMcp.Tools;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.Tools;
using AzureMcp.Tools.WorkItems;

namespace AzureMcp.Tools.Tests.Tools;

public sealed class SearchWorkItemsToolTests
{
    [Fact]
    public async Task ExecuteAsync_MapsClientResultToToolResponse()
    {
        var tool = new SearchWorkItemsTool(new FakeClient(), new ConfiguredState());

        var result = await tool.ExecuteAsync("deploy", top: 5, includeClosed: false, includeDescription: true);

        result.Error.IsNull();
        result.Tickets.Select(ticket => ticket.Id).Is(42, 77);
        result.Tickets.Select(ticket => ticket.Title).Is("Investigate deployment", "Deployment docs");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNotConfigured_WithoutCallingClient()
    {
        var client = new SpyClient();
        var tool = new SearchWorkItemsTool(client, new MissingState());

        var result = await tool.ExecuteAsync("deploy");

        result.Error.IsNotNull();
        result.Tickets.IsEmpty();
        result.Error!.Message.IsContaining("config file");
        client.Calls.Is(0);
    }

    private sealed class FakeClient : IAzureDevOpsWorkItemClient
    {
        public Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("ReadWorkItemAsync should not be called in this test.");

        public Task<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)> SearchWorkItemsAsync(AzureDevOpsConnectionInfo connection, string query, int top = 20, bool includeClosed = false, bool includeDescription = false, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)>((
            [
                new SearchTicketResult(42, "Investigate deployment", "Active", "Bug", "2026-04-26T07:00:00Z"),
                new SearchTicketResult(77, "Deployment docs", "New", "Task", "2026-04-25T07:00:00Z")
            ],
            null));
    }

    private sealed class SpyClient : IAzureDevOpsWorkItemClient
    {
        public int Calls { get; private set; }

        public Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(AzureDevOpsConnectionInfo connection, int workItemId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("ReadWorkItemAsync should not be called in this test.");

        public Task<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)> SearchWorkItemsAsync(AzureDevOpsConnectionInfo connection, string query, int top = 20, bool includeClosed = false, bool includeDescription = false, CancellationToken cancellationToken = default)
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

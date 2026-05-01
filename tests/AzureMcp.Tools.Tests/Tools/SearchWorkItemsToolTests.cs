using AzureMcp.Tools.Tests.Fixtures;
using AzureMcp.Tools.Tools;

namespace AzureMcp.Tools.Tests.Tools;

public sealed class SearchWorkItemsToolTests
{
    [Fact]
    public async Task ExecuteAsync_UsesSharedTicketCorpus_ForTitleMatchesByDefault()
    {
        var client = new FixtureBackedWorkItemClient();
        var tool = new SearchWorkItemsTool(client, new ConfiguredConnectionState());

        var result = await tool.ExecuteAsync("deploy", top: 5, includeClosed: false, includeDescription: false);

        result.Error.IsNull();
        result.Tickets.Select(ticket => ticket.Id).Is(1010, 1015);
        result.Tickets.Select(ticket => ticket.Title).Is(
            "Deploy pipeline fails",
            "Überarbeite Deployment-Handbuch");
        client.SearchCalls.Is(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIncludeDescriptionTrue_AddsDescriptionOnlyMatches_AfterTitleHits()
    {
        var client = new FixtureBackedWorkItemClient();
        var tool = new SearchWorkItemsTool(client, new ConfiguredConnectionState());

        var result = await tool.ExecuteAsync("deploy", top: 5, includeClosed: false, includeDescription: true);

        result.Error.IsNull();
        result.Tickets.Select(ticket => ticket.Id).Is(1010, 1015, 1011);
        result.Tickets.Select(ticket => ticket.Title).Is(
            "Deploy pipeline fails",
            "Überarbeite Deployment-Handbuch",
            "Investigate flaky tests");
        client.SearchCalls.Is(1);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConnectionError_WithoutCallingClient()
    {
        var client = new ThrowingWorkItemClient();
        var tool = new SearchWorkItemsTool(client, new FailingConnectionState());

        var result = await tool.ExecuteAsync("deploy");

        result.Error.IsNotNull();
        result.Tickets.IsEmpty();
        result.Error!.Message.IsContaining("config invalid");
        client.SearchCalls.Is(0);
    }
}

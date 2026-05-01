using AzureMcp.Tools.Tests.Fixtures;
using AzureMcp.Tools.Tools;

namespace AzureMcp.Tools.Tests.Tools;

public sealed class ReadWorkItemToolTests
{
    [Fact]
    public async Task ExecuteAsync_MapsSharedTicketFixtureToToolResponse()
    {
        var client = new FixtureBackedWorkItemClient();
        var pullRequests = new ThrowingPullRequestClient();
        var tool = new ReadWorkItemTool(client, pullRequests, new ConfiguredConnectionState());

        var result = await tool.ExecuteAsync(1001);

        result.Error.IsNull();
        result.Ticket.IsNotNull();
        result.Ticket!.Id.Is(1001);
        result.Ticket.Title.Is("Observability rollout");
        result.Ticket.State.Is("Active");
        result.Ticket.WorkItemType.Is("User Story");
        result.Ticket.AssignedTo.Is("Ada Lovelace");
        client.ReadCalls.Is(1);
        pullRequests.Calls.Is(0);
    }

    [Fact]
    public async Task ExecuteAsync_LoadsLinkedPullRequests_FromSharedFixtures()
    {
        var client = new FixtureBackedWorkItemClient();
        var pullRequests = new FixtureBackedPullRequestClient();
        var tool = new ReadWorkItemTool(client, pullRequests, new ConfiguredConnectionState());

        var result = await tool.ExecuteAsync(1002);

        result.Error.IsNull();
        result.Ticket.IsNotNull();
        result.Ticket!.PullRequests.Select(pr => pr.Id).Is(33, 44);
        result.Ticket.PullRequests.Select(pr => pr.DescriptionText).Is(
            "PR description text",
            "Tightens deployment logging and dashboards.");
        client.ReadCalls.Is(1);
        pullRequests.Calls.Is(2);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConnectionError_WithoutCallingClient()
    {
        var client = new ThrowingWorkItemClient();
        var pullRequests = new ThrowingPullRequestClient();
        var tool = new ReadWorkItemTool(client, pullRequests, new FailingConnectionState());

        var result = await tool.ExecuteAsync(123);

        result.Error.IsNotNull();
        result.Ticket.IsNull();
        result.Error!.Message.IsContaining("config invalid");
        client.ReadCalls.Is(0);
        pullRequests.Calls.Is(0);
    }
}

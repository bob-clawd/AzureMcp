using AzureMcp.Tools.Tests.Fixtures;
using AzureMcp.Tools.Tools;

namespace AzureMcp.Tools.Tests.Tools;

public sealed class GetContextToolTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCalledWithChild_ReturnsParentChainContext()
    {
        var client = new FixtureBackedWorkItemClient();
        var tool = new GetContextTool(client, new ConfiguredConnectionState());

        var result = await tool.ExecuteAsync(1002);

        result.Error.IsNull();
        result.Tickets.Select(item => item.Id).Is(1000, 1001, 1002);
        result.Tickets.Select(item => item.DescriptionText).Is(
            "Root description",
            "Child description",
            "Grandchild description");
        client.ReadCalls.Is(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalledWithRoot_ReturnsOnlyRoot()
    {
        var client = new FixtureBackedWorkItemClient();
        var tool = new GetContextTool(client, new ConfiguredConnectionState());

        var result = await tool.ExecuteAsync(1000);

        result.Error.IsNull();
        result.Tickets.Select(item => item.Id).Is(1000);
        result.Tickets.Select(item => item.DescriptionText).Is("Root description");
        client.ReadCalls.Is(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalledWithSibling_ReturnsRootToSiblingChain()
    {
        var client = new FixtureBackedWorkItemClient();
        var tool = new GetContextTool(client, new ConfiguredConnectionState());

        var result = await tool.ExecuteAsync(1003);

        result.Error.IsNull();
        result.Tickets.Select(item => item.Id).Is(1000, 1003);
        result.Tickets.Select(item => item.DescriptionText).Is("Root description", "Sibling description");
        client.ReadCalls.Is(2);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConfigError_WhenNotConfigured_WithoutCallingClient()
    {
        var client = new ThrowingWorkItemClient();
        var tool = new GetContextTool(client, new MissingConnectionState());

        var result = await tool.ExecuteAsync(1002);

        result.Error.IsNotNull();
        result.Error!.Message.IsContaining("config file");
        client.ReadCalls.Is(0);
    }
}

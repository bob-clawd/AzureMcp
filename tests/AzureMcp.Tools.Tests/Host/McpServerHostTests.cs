using AzureMcp.Host;

namespace AzureMcp.Tools.Tests.Host;

public sealed class McpServerHostTests
{
    [Fact]
    public void ParseOptions_UsesEnvironmentVariables_WhenArgumentsMissing()
    {
        var options = McpServerHost.ParseOptions(["--config", "/tmp/azuremcp.json"], new Dictionary<string, string?>());

        Assert.Equal("/tmp/azuremcp.json", options.ConfigPath);
    }

    [Fact]
    public void ParseOptions_ArgumentsOverrideEnvironmentVariables()
    {
        var options = McpServerHost.ParseOptions(
            ["--config", "/tmp/azuremcp.json"],
            new Dictionary<string, string?>());

        Assert.Equal("/tmp/azuremcp.json", options.ConfigPath);
    }
}

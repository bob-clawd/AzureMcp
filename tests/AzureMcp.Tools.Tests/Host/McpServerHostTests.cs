using AzureMcp.Host;

namespace AzureMcp.Tools.Tests.Host;

public sealed class McpServerHostTests
{
    [Fact]
    public void ParseOptions_ParsesConfigPath()
    {
        var options = McpServerHost.ParseOptions(["--config", "/tmp/azuremcp.json"]);

        Assert.Equal("/tmp/azuremcp.json", options.ConfigPath);
    }

    [Fact]
    public void ParseOptions_NormalizesConfigPath_ToFullPath()
    {
        var options = McpServerHost.ParseOptions(["--config", "./azuremcp.json"]);

        Assert.Equal(Path.GetFullPath("./azuremcp.json"), options.ConfigPath);
    }
}

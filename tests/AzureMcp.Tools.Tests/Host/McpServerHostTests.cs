using AzureMcp.Host;

namespace AzureMcp.Tools.Tests.Host;

public sealed class McpServerHostTests
{
    [Fact]
    public void ParseOptions_AllowsMissingConfigPath()
    {
        var options = McpServerHost.ParseOptions([]);

        options.ConfigPath.IsNull();
    }

    [Fact]
    public void ParseOptions_ParsesConfigPath()
    {
        var options = McpServerHost.ParseOptions(["--config", "/tmp/azuremcp.json"]);

        options.ConfigPath.Is("/tmp/azuremcp.json");
    }

    [Fact]
    public void ParseOptions_NormalizesConfigPath_ToFullPath()
    {
        var options = McpServerHost.ParseOptions(["--config", "./azuremcp.json"]);

        options.ConfigPath.Is(Path.GetFullPath("./azuremcp.json"));
    }
}

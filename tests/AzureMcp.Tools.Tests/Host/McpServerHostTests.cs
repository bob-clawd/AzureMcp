using AzureMcp.Host;

namespace AzureMcp.Tools.Tests.Host;

public sealed class McpServerHostTests
{
    [Fact]
    public void ParseOptions_UsesEnvironmentVariables_WhenArgumentsMissing()
    {
        var environment = new Dictionary<string, string?>
        {
            ["AZURE_MCP_ORGANIZATION_URL"] = "https://dev.azure.com/test-org",
            ["AZURE_MCP_PAT"] = "secret-pat",
            ["AZURE_MCP_PROJECT"] = "PersonalProject"
        };

        var options = McpServerHost.ParseOptions(["--config", "/tmp/azuremcp.json"], environment);

        Assert.Equal("/tmp/azuremcp.json", options.ConfigPath);
        Assert.Equal("https://dev.azure.com/test-org", options.OrganizationUrl);
        Assert.Equal("secret-pat", options.PersonalAccessToken);
        Assert.Equal("PersonalProject", options.Project);
    }

    [Fact]
    public void ParseOptions_ArgumentsOverrideEnvironmentVariables()
    {
        var environment = new Dictionary<string, string?>
        {
            ["AZURE_MCP_ORGANIZATION_URL"] = "https://dev.azure.com/env-org",
            ["AZURE_MCP_PAT"] = "env-pat"
        };

        var options = McpServerHost.ParseOptions(
            ["--config", "/tmp/azuremcp.json", "--organization-url", "https://dev.azure.com/cli-org", "--pat", "cli-pat"],
            environment);

        Assert.Equal("/tmp/azuremcp.json", options.ConfigPath);
        Assert.Equal("https://dev.azure.com/cli-org", options.OrganizationUrl);
        Assert.Equal("cli-pat", options.PersonalAccessToken);
    }
}

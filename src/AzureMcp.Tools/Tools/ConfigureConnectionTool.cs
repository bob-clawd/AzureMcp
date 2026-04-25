using System.ComponentModel;
using AzureMcp.Tools.Configuration;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools.Tools;

public sealed record ConfigureConnectionResponse(
    bool OrganizationUrlSet,
    bool PersonalAccessTokenSet,
    bool ProjectSet,
    string? Error);

public sealed class ConfigureConnectionTool(IAzureDevOpsConnectionState state) : Tool
{
    [McpServerTool(Name = "configure_connection", Title = "Configure Azure DevOps Connection", ReadOnly = false, Idempotent = true)]
    [Description("Configure AzureMcp with Azure DevOps connection values for this MCP server process. Use this after asking the user for missing values.")]
    public Task<ConfigureConnectionResponse> ExecuteAsync(
        [Description("Azure DevOps organization URL, e.g. https://dev.azure.com/your-org")] string? organizationUrl = null,
        [Description("Azure DevOps Personal Access Token (PAT)")] string? personalAccessToken = null,
        [Description("Optional Azure DevOps project name")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var org = string.IsNullOrWhiteSpace(organizationUrl) ? null : organizationUrl.Trim();
        var pat = string.IsNullOrWhiteSpace(personalAccessToken) ? null : personalAccessToken.Trim();
        var proj = string.IsNullOrWhiteSpace(project) ? null : project.Trim();

        if (org is not null && (!Uri.TryCreate(org, UriKind.Absolute, out var parsed) || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)))
        {
            return Task.FromResult(new ConfigureConnectionResponse(
                OrganizationUrlSet: false,
                PersonalAccessTokenSet: false,
                ProjectSet: false,
                Error: $"Invalid organizationUrl '{org}'. Expected an absolute http/https URL."));
        }

        org = org?.TrimEnd('/');

        state.Set(org, pat, proj);

        var orgSet = org is not null;
        var patSet = pat is not null;
        var projSet = proj is not null;

        if (orgSet) Environment.SetEnvironmentVariable("AZURE_MCP_ORGANIZATION_URL", org);
        if (patSet) Environment.SetEnvironmentVariable("AZURE_MCP_PAT", pat);
        if (projSet) Environment.SetEnvironmentVariable("AZURE_MCP_PROJECT", proj);

        return Task.FromResult(new ConfigureConnectionResponse(
            OrganizationUrlSet: orgSet,
            PersonalAccessTokenSet: patSet,
            ProjectSet: projSet,
            Error: null));
    }
}

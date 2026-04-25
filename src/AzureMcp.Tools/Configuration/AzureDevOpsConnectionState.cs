namespace AzureMcp.Tools.Configuration;

internal sealed class AzureDevOpsConnectionState : IAzureDevOpsConnectionState
{
    private readonly object _gate = new();

    private string? _organizationUrl;
    private string? _personalAccessToken;
    private string? _project;

    public AzureDevOpsConnectionState(string? organizationUrl, string? personalAccessToken, string? project)
    {
        _organizationUrl = NormalizeUrl(organizationUrl);
        _personalAccessToken = NormalizeString(personalAccessToken);
        _project = NormalizeString(project);
    }

    public void Set(string? organizationUrl, string? personalAccessToken, string? project)
    {
        lock (_gate)
        {
            _organizationUrl = NormalizeUrl(organizationUrl) ?? _organizationUrl;
            _personalAccessToken = NormalizeString(personalAccessToken) ?? _personalAccessToken;
            _project = NormalizeString(project) ?? _project;
        }
    }

    public AzureDevOpsConnectionInfo GetRequired()
    {
        if (TryGetRequired(out var connection, out var missing))
            return connection;

        throw new AzureMcpConfigurationException(missing);
    }

    public bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out IReadOnlyList<string> missingEnvironmentVariables)
    {
        lock (_gate)
        {
            var org = _organizationUrl ?? NormalizeUrl(Environment.GetEnvironmentVariable("AZURE_MCP_ORGANIZATION_URL"));
            var pat = _personalAccessToken ?? NormalizeString(Environment.GetEnvironmentVariable("AZURE_MCP_PAT"));
            var project = _project ?? NormalizeString(Environment.GetEnvironmentVariable("AZURE_MCP_PROJECT"));

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(org)) missing.Add("AZURE_MCP_ORGANIZATION_URL");
            if (string.IsNullOrWhiteSpace(pat)) missing.Add("AZURE_MCP_PAT");

            if (missing.Count > 0)
            {
                connection = default!;
                missingEnvironmentVariables = missing;
                return false;
            }

            connection = new AzureDevOpsConnectionInfo(
                OrganizationUrl: org!,
                PersonalAccessToken: pat!,
                Project: project);

            missingEnvironmentVariables = Array.Empty<string>();
            return true;
        }
    }

    private static string? NormalizeString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeUrl(string? value)
    {
        value = NormalizeString(value);
        return value is null ? null : value.TrimEnd('/');
    }
}

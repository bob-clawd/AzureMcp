namespace AzureMcp.Tools.Configuration;

public interface IAzureDevOpsConnectionState
{
    AzureDevOpsConnectionInfo GetRequired();

    bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out IReadOnlyList<string> missingEnvironmentVariables);

    void Set(string? organizationUrl, string? personalAccessToken, string? project);
}

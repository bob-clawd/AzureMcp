namespace AzureMcp.Tools.Configuration;

public interface IAzureDevOpsConnectionState
{
    string ConfigPath { get; }

    AzureDevOpsConnectionInfo GetRequired();

    bool TryGetRequired(
        out AzureDevOpsConnectionInfo connection,
        out ErrorInfo? error,
        out IReadOnlyList<string>? missingConfigKeys);

    bool TryPersist(out ErrorInfo? error);

    void Set(string? organizationUrl, string? personalAccessToken, string? project);
}

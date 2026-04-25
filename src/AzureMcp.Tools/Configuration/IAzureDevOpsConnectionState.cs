namespace AzureMcp.Tools.Configuration;

public interface IAzureDevOpsConnectionState
{
    AzureDevOpsConnectionInfo GetRequired();

    void Set(string? organizationUrl, string? personalAccessToken, string? project);
}

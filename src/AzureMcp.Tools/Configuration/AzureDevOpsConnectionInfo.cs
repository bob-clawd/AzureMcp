namespace AzureMcp.Tools.Configuration;

public sealed record AzureDevOpsConnectionInfo(
    string OrganizationUrl,
    string? PersonalAccessToken,
    string? Project);

namespace AzureMcp.Tools.Configuration;

internal sealed class AzureDevOpsConnectionOptions
{
    public required string OrganizationUrl { get; init; }

    public string? Project { get; init; }

    public required string PersonalAccessToken { get; init; }
}

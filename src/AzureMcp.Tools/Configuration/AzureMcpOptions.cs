namespace AzureMcp.Tools.Configuration;

public sealed class AzureMcpOptions
{
    public required string OrganizationUrl { get; init; }

    public string? Project { get; init; }

    public required string PersonalAccessToken { get; init; }
}

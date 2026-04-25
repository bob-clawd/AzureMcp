namespace AzureMcp.Host;

internal sealed class AzureMcpOptions
{
    public required string ConfigPath { get; init; }

    public string? OrganizationUrl { get; init; }

    public string? Project { get; init; }

    public string? PersonalAccessToken { get; init; }
}

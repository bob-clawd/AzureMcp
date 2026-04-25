namespace AzureMcp.Host;

internal sealed class AzureMcpOptions
{
    public string? OrganizationUrl { get; init; }

    public string? Project { get; init; }

    public string? PersonalAccessToken { get; init; }
}

using System.Collections.Concurrent;

namespace AzureMcp.Tools.Clients;

internal sealed class AzureDevOpsAuthState : IAzureDevOpsAuthState
{
    private readonly ConcurrentDictionary<string, AzureDevOpsAuthMode> overrides = new(StringComparer.OrdinalIgnoreCase);

    public AzureDevOpsAuthMode GetMode(string organizationUrl, bool hasPersonalAccessToken)
    {
        if (overrides.TryGetValue(organizationUrl, out var mode))
            return mode;

        return hasPersonalAccessToken
            ? AzureDevOpsAuthMode.PatThenWindows
            : AzureDevOpsAuthMode.WindowsOnly;
    }

    public void RememberWindowsOnly(string organizationUrl)
        => overrides[organizationUrl] = AzureDevOpsAuthMode.WindowsOnly;
}

namespace AzureMcp.Tools.Clients;

internal interface IAzureDevOpsAuthState
{
    AzureDevOpsAuthMode GetMode(string organizationUrl, bool hasPersonalAccessToken);

    void RememberWindowsOnly(string organizationUrl);
}

internal enum AzureDevOpsAuthMode
{
    WindowsOnly,
    PatThenWindows
}

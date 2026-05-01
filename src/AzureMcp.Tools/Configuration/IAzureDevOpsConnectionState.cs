namespace AzureMcp.Tools.Configuration;

public interface IAzureDevOpsConnectionState
{
    string? ConfigPath { get; }

    bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out ErrorInfo? error);
}

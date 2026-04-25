namespace AzureMcp.Tools.Configuration;

public sealed class AzureMcpConfigurationException : InvalidOperationException
{
    public AzureMcpConfigurationException(IReadOnlyList<string> missingKeys)
        : base(BuildMessage(missingKeys))
    {
        MissingKeys = missingKeys;
    }

    public IReadOnlyList<string> MissingKeys { get; }

    private static string BuildMessage(IReadOnlyList<string> missingKeys)
    {
        var keys = string.Join(", ", missingKeys);

        return $"AzureMcp is not configured. Missing: {keys}. " +
               "Ask the user for the missing value(s), then call `configure_connection` with the provided values (this sets them for the current MCP server process).";
    }
}

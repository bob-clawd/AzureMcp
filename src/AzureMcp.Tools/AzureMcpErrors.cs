namespace AzureMcp.Tools;

public static class AzureMcpErrors
{
    public static ErrorInfo MissingConfig(string configPath, IReadOnlyList<string> missingConfigKeys)
    {
        var keys = string.Join(", ", missingConfigKeys);
        return new ErrorInfo(
            $"AzureMcp is not configured. Config file: '{configPath}'. Missing: {keys}. Ask the user for the value(s), then call `configure_connection` with the provided values (this writes the config file).");
    }
}

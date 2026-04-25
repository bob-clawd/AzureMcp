namespace AzureMcp.Tools;

public static class AzureMcpErrors
{
    public static ErrorInfo MissingConfig(IReadOnlyList<string> missingEnvironmentVariables)
    {
        var keys = string.Join(", ", missingEnvironmentVariables);
        return new ErrorInfo(
            $"AzureMcp is not configured. Missing: {keys}. Ask the user for the value(s), then call `configure_connection` with the provided values.");
    }
}

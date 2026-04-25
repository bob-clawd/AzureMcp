namespace AzureMcp.Tools;

public sealed record ErrorInfo(
    string Message,
    IReadOnlyDictionary<string, string>? Details = null);

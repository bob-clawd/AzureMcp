using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Clients;

public interface IAzureDevOpsRequestDispatcher
{
    Task<HttpResponseMessage> SendAsync(
        AzureDevOpsConnectionInfo connection,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken = default);
}

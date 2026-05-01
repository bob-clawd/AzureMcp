using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AzureMcp.Tools.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureMcp.Tools.Clients;

internal sealed class AzureDevOpsRequestDispatcher(
    IHttpClientFactory httpClientFactory,
    IAzureDevOpsAuthState authState) : IAzureDevOpsRequestDispatcher
{
    public const string PatClientName = "AzureDevOps.Pat";
    public const string WindowsClientName = "AzureDevOps.Windows";

    public async Task<HttpResponseMessage> SendAsync(
        AzureDevOpsConnectionInfo connection,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken = default)
    {
        var hasPat = !string.IsNullOrWhiteSpace(connection.PersonalAccessToken);
        var mode = authState.GetMode(connection.OrganizationUrl, hasPat);

        if (mode == AzureDevOpsAuthMode.WindowsOnly)
            return await SendWithWindowsAsync(requestFactory, cancellationToken).ConfigureAwait(false);

        var patResponse = await SendWithPatAsync(connection.PersonalAccessToken!, requestFactory, cancellationToken).ConfigureAwait(false);
        if (!IsAuthFailure(patResponse.StatusCode))
            return patResponse;

        patResponse.Dispose();

        var windowsResponse = await SendWithWindowsAsync(requestFactory, cancellationToken).ConfigureAwait(false);
        if (!IsAuthFailure(windowsResponse.StatusCode))
            authState.RememberWindowsOnly(connection.OrganizationUrl);

        return windowsResponse;
    }

    private async Task<HttpResponseMessage> SendWithPatAsync(
        string personalAccessToken,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        using var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

        return await httpClientFactory.CreateClient(PatClientName)
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendWithWindowsAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        using var request = requestFactory();
        return await httpClientFactory.CreateClient(WindowsClientName)
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsAuthFailure(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}

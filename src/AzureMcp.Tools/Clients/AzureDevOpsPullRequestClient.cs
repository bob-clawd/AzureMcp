using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Clients;

public sealed class AzureDevOpsPullRequestClient(IAzureDevOpsRequestDispatcher requestDispatcher) : IAzureDevOpsPullRequestClient
{
    public async Task<(PullRequestInfo? PullRequest, ErrorInfo? Error)> ReadPullRequestAsync(
        AzureDevOpsConnectionInfo connection,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default)
    {
        if (pullRequestId <= 0)
        {
            return AsError(
                "pullRequestId must be greater than zero",
                new Dictionary<string, string>
                {
                    ["pullRequestId"] = pullRequestId.ToString(),
                    ["projectId"] = projectId,
                    ["repositoryId"] = repositoryId
                });
        }

        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(repositoryId))
        {
            return AsError(
                "projectId and repositoryId must be specified",
                new Dictionary<string, string>
                {
                    ["pullRequestId"] = pullRequestId.ToString(),
                    ["projectId"] = projectId,
                    ["repositoryId"] = repositoryId
                });
        }

        var requestUrl = $"{connection.OrganizationUrl}/{projectId}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}?api-version=7.1";

        HttpResponseMessage response;
        try
        {
            response = await requestDispatcher.SendAsync(
                connection,
                () => CreateJsonRequest(requestUrl),
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return AsError(
                "Azure DevOps Git request failed (network/infrastructure error)",
                new Dictionary<string, string>
                {
                    ["pullRequestId"] = pullRequestId.ToString(),
                    ["projectId"] = projectId,
                    ["repositoryId"] = repositoryId,
                    ["url"] = requestUrl,
                    ["exception"] = ex.GetType().Name,
                    ["message"] = ex.Message
                });
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return AsError(
                    "pull request not found",
                    new Dictionary<string, string>
                    {
                        ["pullRequestId"] = pullRequestId.ToString(),
                        ["projectId"] = projectId,
                        ["repositoryId"] = repositoryId,
                        ["status"] = ((int)response.StatusCode).ToString(),
                        ["url"] = requestUrl
                    });
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return AsError(
                    "Azure DevOps Git request not authorized. Check Windows authentication and PAT configuration.",
                    new Dictionary<string, string>
                    {
                        ["pullRequestId"] = pullRequestId.ToString(),
                        ["projectId"] = projectId,
                        ["repositoryId"] = repositoryId,
                        ["status"] = ((int)response.StatusCode).ToString(),
                        ["url"] = requestUrl
                    });
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await TryReadBodySnippet(response, cancellationToken).ConfigureAwait(false);
                var details = new Dictionary<string, string>
                {
                    ["pullRequestId"] = pullRequestId.ToString(),
                    ["projectId"] = projectId,
                    ["repositoryId"] = repositoryId,
                    ["status"] = ((int)response.StatusCode).ToString(),
                    ["reason"] = response.ReasonPhrase ?? string.Empty,
                    ["url"] = requestUrl
                };

                if (!string.IsNullOrWhiteSpace(body))
                    details["bodySnippet"] = body;

                return AsError(
                    $"Azure DevOps Git request failed ({(int)response.StatusCode} {response.StatusCode})",
                    details);
            }

            try
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                var root = document.RootElement;
                var description = root.TryGetProperty("description", out var descriptionElement)
                    ? descriptionElement.GetString()
                    : null;

                return (new PullRequestInfo(pullRequestId, description), null);
            }
            catch (JsonException ex)
            {
                return AsError(
                    "Azure DevOps pull request response could not be parsed as JSON",
                    new Dictionary<string, string>
                    {
                        ["pullRequestId"] = pullRequestId.ToString(),
                        ["projectId"] = projectId,
                        ["repositoryId"] = repositoryId,
                        ["url"] = requestUrl,
                        ["exception"] = ex.GetType().Name,
                        ["message"] = ex.Message
                    });
            }
        }
    }

    private static (PullRequestInfo? PullRequest, ErrorInfo? Error) AsError(
        string message,
        IReadOnlyDictionary<string, string>? details = null)
        => (null, new ErrorInfo(message, details));

    private static HttpRequestMessage CreateJsonRequest(string requestUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task<string?> TryReadBodySnippet(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
                return null;

            body = body.Replace("\r", string.Empty);
            return body.Length <= 800 ? body : body[..800];
        }
        catch
        {
            return null;
        }
    }
}

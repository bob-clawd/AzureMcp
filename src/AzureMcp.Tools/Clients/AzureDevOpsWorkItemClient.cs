using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Clients;

public sealed class AzureDevOpsWorkItemClient(IAzureDevOpsRequestDispatcher requestDispatcher) : IAzureDevOpsWorkItemClient
{
    private static readonly HashSet<string> ClosedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Closed",
        "Done",
        "Removed"
    };

    public async Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        if (workItemId <= 0)
            return AsError(
                "workItemId must be greater than zero",
                new Dictionary<string, string> { ["workItemId"] = workItemId.ToString() });

        var requestUrl = $"{connection.OrganizationUrl}/_apis/wit/workitems/{workItemId}?$expand=all&api-version=7.1";

        HttpResponseMessage response;
        try
        {
            response = await requestDispatcher.SendAsync(
                connection,
                () => CreateJsonRequest(HttpMethod.Get, requestUrl),
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return AsError(
                "Azure DevOps request failed (network/infrastructure error)",
                new Dictionary<string, string>
                {
                    ["workItemId"] = workItemId.ToString(),
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
                    "work item not found",
                    new Dictionary<string, string>
                    {
                        ["workItemId"] = workItemId.ToString(),
                        ["status"] = ((int)response.StatusCode).ToString(),
                        ["url"] = requestUrl
                    });
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return AsError(
                    "Azure DevOps request not authorized. Check Windows authentication and PAT configuration.",
                    new Dictionary<string, string>
                    {
                        ["workItemId"] = workItemId.ToString(),
                        ["status"] = ((int)response.StatusCode).ToString(),
                        ["url"] = requestUrl
                    });
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await TryReadBodySnippet(response, cancellationToken).ConfigureAwait(false);
                var details = new Dictionary<string, string>
                {
                    ["workItemId"] = workItemId.ToString(),
                    ["status"] = ((int)response.StatusCode).ToString(),
                    ["reason"] = response.ReasonPhrase ?? string.Empty,
                    ["url"] = requestUrl
                };

                if (!string.IsNullOrWhiteSpace(body))
                    details["bodySnippet"] = body;

                return AsError(
                    $"Azure DevOps request failed ({(int)response.StatusCode} {response.StatusCode})",
                    details);
            }

            try
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                var ticket = Parse(document.RootElement);
                return (ticket, null);
            }
            catch (JsonException ex)
            {
                return AsError(
                    "Azure DevOps response could not be parsed as JSON",
                    new Dictionary<string, string>
                    {
                        ["workItemId"] = workItemId.ToString(),
                        ["url"] = requestUrl,
                        ["exception"] = ex.GetType().Name,
                        ["message"] = ex.Message
                    });
            }
        }
    }

    public async Task<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)> SearchWorkItemsAsync(
        AzureDevOpsConnectionInfo connection,
        string query,
        int top = 20,
        bool includeClosed = false,
        bool includeDescription = false,
        CancellationToken cancellationToken = default)
    {
        query = query?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            return (null, new ErrorInfo(
                "query must not be empty",
                new Dictionary<string, string> { ["query"] = query }));
        }

        if (top <= 0)
        {
            return (null, new ErrorInfo(
                "top must be greater than zero",
                new Dictionary<string, string> { ["top"] = top.ToString() }));
        }

        var requestUrl = BuildSearchUrl(connection);
        var requestTop = includeClosed ? top : Math.Min(top * 3, 100);
        var requestPayload = new Dictionary<string, object?>
        {
            ["searchText"] = query,
            ["$skip"] = 0,
            ["$top"] = requestTop,
            ["$orderBy"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["field"] = "system.changeddate",
                    ["sortOrder"] = "DESC"
                }
            },
            ["includeFacets"] = false
        };

        var requestBody = JsonSerializer.Serialize(requestPayload);

        HttpResponseMessage response;
        try
        {
            response = await requestDispatcher.SendAsync(
                connection,
                () => CreateJsonRequest(
                    HttpMethod.Post,
                    requestUrl,
                    new StringContent(requestBody, Encoding.UTF8, "application/json")),
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return (null, new ErrorInfo(
                "Azure DevOps search request failed (network/infrastructure error)",
                new Dictionary<string, string>
                {
                    ["query"] = query,
                    ["url"] = requestUrl,
                    ["exception"] = ex.GetType().Name,
                    ["message"] = ex.Message
                }));
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return (null, new ErrorInfo(
                    "Azure DevOps search request not authorized. Check Windows authentication and PAT configuration.",
                    new Dictionary<string, string>
                    {
                        ["query"] = query,
                        ["status"] = ((int)response.StatusCode).ToString(),
                        ["url"] = requestUrl
                    }));
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await TryReadBodySnippet(response, cancellationToken).ConfigureAwait(false);
                var details = new Dictionary<string, string>
                {
                    ["query"] = query,
                    ["status"] = ((int)response.StatusCode).ToString(),
                    ["reason"] = response.ReasonPhrase ?? string.Empty,
                    ["url"] = requestUrl
                };

                if (!string.IsNullOrWhiteSpace(body))
                    details["bodySnippet"] = body;

                return (null, new ErrorInfo(
                    $"Azure DevOps search request failed ({(int)response.StatusCode} {response.StatusCode})",
                    details));
            }

            try
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                var results = ParseSearchResults(document.RootElement, query, top, includeClosed, includeDescription);
                return (results, null);
            }
            catch (JsonException ex)
            {
                return (null, new ErrorInfo(
                    "Azure DevOps search response could not be parsed as JSON",
                    new Dictionary<string, string>
                    {
                        ["query"] = query,
                        ["url"] = requestUrl,
                        ["exception"] = ex.GetType().Name,
                        ["message"] = ex.Message
                    }));
            }
        }
    }

    private static (Ticket? Ticket, ErrorInfo? Error) AsError(
        string message,
        IReadOnlyDictionary<string, string>? details = null)
        => (null, new ErrorInfo(message, details));

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string requestUrl, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, requestUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = content;
        return request;
    }

    private static string BuildSearchUrl(AzureDevOpsConnectionInfo connection)
    {
        var organizationUri = new Uri(connection.OrganizationUrl, UriKind.Absolute);
        var host = string.Equals(organizationUri.Host, "dev.azure.com", StringComparison.OrdinalIgnoreCase)
            ? "almsearch.dev.azure.com"
            : organizationUri.Host;

        var path = organizationUri.AbsolutePath.Trim('/');
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(path))
            segments.Add(path);

        if (!string.IsNullOrWhiteSpace(connection.Project))
            segments.Add(Uri.EscapeDataString(connection.Project));

        var joinedPath = string.Join('/', segments);
        return $"{organizationUri.Scheme}://{host}/{joinedPath}/_apis/search/workitemsearchresults?api-version=7.1";
    }

    internal static IReadOnlyList<SearchTicketResult> ParseSearchResults(
        JsonElement root,
        string query,
        int top,
        bool includeClosed,
        bool includeDescription)
    {
        if (!root.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<SearchTicketResult>();

        var normalizedQuery = query.Trim();
        var candidates = new List<SearchCandidate>();

        foreach (var result in resultsElement.EnumerateArray())
        {
            if (!result.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
                continue;

            var idText = GetString(fields, "system.id");
            if (!int.TryParse(idText, out var id))
                continue;

            var title = GetString(fields, "system.title");
            var state = GetString(fields, "system.state");
            var workItemType = GetString(fields, "system.workitemtype");
            var changedDate = GetString(fields, "system.changeddate");
            var changedDateValue = DateTimeOffset.TryParse(changedDate, out var parsedDate)
                ? parsedDate
                : DateTimeOffset.MinValue;

            var hitFields = GetHitFields(result);
            var hasTitleHit = hitFields.Contains("system.title")
                || (!string.IsNullOrWhiteSpace(title)
                    && title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
            var hasDescriptionHit = hitFields.Contains("system.description");
            var isClosed = IsClosedState(state);

            if (!hasTitleHit && !(includeDescription && hasDescriptionHit))
                continue;

            if (!includeClosed && isClosed)
                continue;

            candidates.Add(new SearchCandidate(
                new SearchTicketResult(id, title, state, workItemType, changedDate),
                hasTitleHit,
                hasDescriptionHit,
                isClosed,
                changedDateValue));
        }

        return candidates
            .OrderBy(candidate => candidate.IsClosed)
            .ThenByDescending(candidate => candidate.HasTitleHit)
            .ThenByDescending(candidate => candidate.HasDescriptionHit)
            .ThenByDescending(candidate => candidate.ChangedDate)
            .Take(top)
            .Select(static candidate => candidate.Result)
            .ToArray();
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

    private static HashSet<string> GetHitFields(JsonElement result)
    {
        if (!result.TryGetProperty("hits", out var hitsElement) || hitsElement.ValueKind != JsonValueKind.Array)
            return [];

        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hit in hitsElement.EnumerateArray())
        {
            var fieldReferenceName = hit.TryGetProperty("fieldReferenceName", out var fieldElement)
                ? fieldElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(fieldReferenceName))
                fields.Add(fieldReferenceName);
        }

        return fields;
    }

    private static bool IsClosedState(string? state)
        => !string.IsNullOrWhiteSpace(state) && ClosedStates.Contains(state);

    internal static Ticket Parse(JsonElement root)
    {
        var fields = root.TryGetProperty("fields", out var fieldsElement) ? fieldsElement : default;
        var descriptionHtml = GetString(fields, "System.Description");

        var relations = root.TryGetProperty("relations", out var relationsElement) ? relationsElement : default;
        var parentId = TryGetLinkedWorkItemId(relations, "System.LinkTypes.Hierarchy-Reverse");
        var childIds = GetLinkedWorkItemIds(relations, "System.LinkTypes.Hierarchy-Forward");
        var branches = GetLinkedGitBranches(relations);
        var pullRequestRefs = GetLinkedPullRequestRefs(relations);

        return new Ticket
        {
            Id = root.GetProperty("id").GetInt32(),
            Title = GetString(fields, "System.Title"),
            State = GetString(fields, "System.State"),
            WorkItemType = GetString(fields, "System.WorkItemType"),
            DescriptionText = ToPlainText(descriptionHtml),
            AssignedTo = ParseAssignedTo(fields),
            ParentId = parentId,
            ChildrenIds = childIds,
            Branches = branches,
            PullRequestRefs = pullRequestRefs
        };
    }

    private static IReadOnlyList<string> GetLinkedGitBranches(JsonElement relations)
    {
        if (relations.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var branches = new List<string>();

        foreach (var relation in relations.EnumerateArray())
        {
            if (!TryGetRelationUrl(relation, "ArtifactLink", out var url))
                continue;

            var branch = TryParseGitBranchFromArtifactUrl(url);
            if (!string.IsNullOrWhiteSpace(branch))
                branches.Add(branch);
        }

        return branches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<PullRequestRef> GetLinkedPullRequestRefs(JsonElement relations)
    {
        if (relations.ValueKind != JsonValueKind.Array)
            return Array.Empty<PullRequestRef>();

        var refs = new List<PullRequestRef>();

        foreach (var relation in relations.EnumerateArray())
        {
            if (!TryGetRelationUrl(relation, "ArtifactLink", out var url))
                continue;

            var parsed = TryParsePullRequestRefFromArtifactUrl(url);
            if (parsed is not null)
                refs.Add(parsed);
        }

        return refs
            .Distinct()
            .OrderBy(static x => x.PullRequestId)
            .ToArray();
    }

    private static bool TryGetRelationUrl(JsonElement relation, string expectedRel, out string? url)
    {
        url = null;

        if (!relation.TryGetProperty("rel", out var relElement))
            return false;

        if (!string.Equals(relElement.GetString(), expectedRel, StringComparison.Ordinal))
            return false;

        if (!relation.TryGetProperty("url", out var urlElement))
            return false;

        url = urlElement.GetString();
        return !string.IsNullOrWhiteSpace(url);
    }

    private static PullRequestRef? TryParsePullRequestRefFromArtifactUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        const string prefix = "vstfs:///Git/PullRequestId/";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var tail = Uri.UnescapeDataString(url[prefix.Length..]);
        var parts = tail.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return null;

        var projectId = parts[0];
        var repositoryId = parts[1];
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(repositoryId))
            return null;

        return int.TryParse(parts[^1], out var id)
            ? new PullRequestRef(projectId, repositoryId, id)
            : null;
    }

    private static string? TryParseGitBranchFromArtifactUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        const string prefix = "vstfs:///Git/Ref/";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var tail = Uri.UnescapeDataString(url[prefix.Length..]);
        var parts = tail.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        var refName = parts.Length >= 3
            ? string.Join('/', parts.Skip(2))
            : parts[^1];

        const string heads = "refs/heads/";
        if (refName.StartsWith(heads, StringComparison.OrdinalIgnoreCase))
            return refName[heads.Length..];

        return refName;
    }

    private static int? TryGetLinkedWorkItemId(JsonElement relations, string relType)
    {
        var ids = GetLinkedWorkItemIds(relations, relType);
        return ids.Count == 0 ? null : ids[0];
    }

    private static IReadOnlyList<int> GetLinkedWorkItemIds(JsonElement relations, string relType)
    {
        if (relations.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();

        var ids = new List<int>();

        foreach (var relation in relations.EnumerateArray())
        {
            if (!relation.TryGetProperty("rel", out var relElement))
                continue;

            if (!string.Equals(relElement.GetString(), relType, StringComparison.Ordinal))
                continue;

            if (!relation.TryGetProperty("url", out var urlElement))
                continue;

            var id = TryParseWorkItemIdFromUrl(urlElement.GetString());
            if (id is not null)
                ids.Add(id.Value);
        }

        return ids
            .Distinct()
            .OrderBy(static x => x)
            .ToArray();
    }

    private static int? TryParseWorkItemIdFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Example:
        // https://dev.azure.com/{org}/_apis/wit/workItems/12345
        var lastSlash = url.LastIndexOf('/');
        if (lastSlash < 0 || lastSlash + 1 >= url.Length)
            return null;

        var tail = url[(lastSlash + 1)..];
        return int.TryParse(tail, out var id) ? id : null;
    }

    private static string? ParseAssignedTo(JsonElement fields)
    {
        if (!fields.TryGetProperty("System.AssignedTo", out var assignedToElement)
            || assignedToElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (assignedToElement.ValueKind == JsonValueKind.String)
            return assignedToElement.GetString();

        var displayName = assignedToElement.TryGetProperty("displayName", out var displayNameElement)
            ? displayNameElement.GetString()
            : null;

        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        var uniqueName = assignedToElement.TryGetProperty("uniqueName", out var uniqueNameElement)
            ? uniqueNameElement.GetString()
            : null;

        return uniqueName;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind != JsonValueKind.Undefined && element.TryGetProperty(propertyName, out var value)
            ? value.GetString()
            : null;

    internal static string? ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var text = html;
        text = Regex.Replace(text, @"<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*/p\s*>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*/div\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);

        var normalizedLines = text
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();

        return string.Join('\n', normalizedLines)
            .Replace("\n\n\n", "\n\n")
            .Trim();
    }

    private sealed record SearchCandidate(
        SearchTicketResult Result,
        bool HasTitleHit,
        bool HasDescriptionHit,
        bool IsClosed,
        DateTimeOffset ChangedDate);
}

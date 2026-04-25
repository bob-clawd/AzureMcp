using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.WorkItems;

public sealed class AzureDevOpsWorkItemClient(HttpClient httpClient, IAzureDevOpsConnectionState connectionState) : IAzureDevOpsWorkItemClient
{
    public async Task<AzureDevOpsWorkItem> ReadWorkItemAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        if (workItemId <= 0)
            throw new ArgumentOutOfRangeException(nameof(workItemId), workItemId, "Work item id must be greater than zero.");

        var connection = connectionState.GetRequired();
        var requestUrl = $"{connection.OrganizationUrl}/_apis/wit/workitems/{workItemId}?$expand=all&api-version=7.1";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{connection.PersonalAccessToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);

        using var response = await httpClient.SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new AzureDevOpsWorkItemNotFoundException(workItemId);

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return Parse(document.RootElement);
    }

    internal static AzureDevOpsWorkItem Parse(JsonElement root)
    {
        var fields = root.TryGetProperty("fields", out var fieldsElement) ? fieldsElement : default;
        var descriptionHtml = GetString(fields, "System.Description");

        var relations = root.TryGetProperty("relations", out var relationsElement) ? relationsElement : default;
        var parentId = TryGetLinkedWorkItemId(relations, "System.LinkTypes.Hierarchy-Reverse");
        var childIds = GetLinkedWorkItemIds(relations, "System.LinkTypes.Hierarchy-Forward");
        var relatedIds = GetLinkedWorkItemIds(relations, "System.LinkTypes.Related");

        return new AzureDevOpsWorkItem(
            Id: root.GetProperty("id").GetInt32(),
            Title: GetString(fields, "System.Title"),
            State: GetString(fields, "System.State"),
            WorkItemType: GetString(fields, "System.WorkItemType"),
            DescriptionText: ToPlainText(descriptionHtml),
            DescriptionHtml: descriptionHtml,
            AssignedTo: ParseAssignedTo(fields),
            ParentWorkItemId: parentId,
            ChildWorkItemIds: childIds,
            RelatedWorkItemIds: relatedIds,
            Url: root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null);
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

    private static AzureDevOpsAssignedTo? ParseAssignedTo(JsonElement fields)
    {
        if (!fields.TryGetProperty("System.AssignedTo", out var assignedToElement)
            || assignedToElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (assignedToElement.ValueKind == JsonValueKind.String)
            return new AzureDevOpsAssignedTo(assignedToElement.GetString(), null);

        return new AzureDevOpsAssignedTo(
            DisplayName: assignedToElement.TryGetProperty("displayName", out var displayName) ? displayName.GetString() : null,
            UniqueName: assignedToElement.TryGetProperty("uniqueName", out var uniqueName) ? uniqueName.GetString() : null);
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
}

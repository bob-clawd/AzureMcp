using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AzureMcp.Tools.WorkItems;

public sealed class AzureDevOpsWorkItemClient(HttpClient httpClient) : IAzureDevOpsWorkItemClient
{
    public async Task<AzureDevOpsWorkItem> ReadWorkItemAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "Work item id must be greater than zero.");

        using var response = await httpClient.GetAsync($"_apis/wit/workitems/{id}?$expand=fields&api-version=7.1", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new AzureDevOpsWorkItemNotFoundException(id);

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return Parse(document.RootElement);
    }

    internal static AzureDevOpsWorkItem Parse(JsonElement root)
    {
        var fields = root.TryGetProperty("fields", out var fieldsElement) ? fieldsElement : default;
        var descriptionHtml = GetString(fields, "System.Description");

        return new AzureDevOpsWorkItem(
            Id: root.GetProperty("id").GetInt32(),
            Title: GetString(fields, "System.Title"),
            State: GetString(fields, "System.State"),
            WorkItemType: GetString(fields, "System.WorkItemType"),
            DescriptionText: ToPlainText(descriptionHtml),
            DescriptionHtml: descriptionHtml,
            AssignedTo: ParseAssignedTo(fields),
            Url: root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null);
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

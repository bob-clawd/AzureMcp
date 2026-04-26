using AzureMcp.Tools;
using AzureMcp.Tools.Clients;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Tests.Fixtures;

internal sealed class FixtureBackedWorkItemClient(FixtureData? fixtures = null) : IAzureDevOpsWorkItemClient
{
    private static readonly HashSet<string> ClosedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Closed",
        "Done",
        "Removed"
    };

    private readonly FixtureData _fixtures = fixtures ?? FixtureCatalog.Load();

    public int ReadCalls { get; private set; }

    public int SearchCalls { get; private set; }

    public Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        ReadCalls++;

        return Task.FromResult<(Ticket? Ticket, ErrorInfo? Error)>(
            _fixtures.Tickets.TryGetValue(workItemId, out var ticket)
                ? (ticket.ToTicket(), null)
                : (null, new ErrorInfo("work item not found")));
    }

    public Task<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)> SearchWorkItemsAsync(
        AzureDevOpsConnectionInfo connection,
        string query,
        int top = 20,
        bool includeClosed = false,
        bool includeDescription = false,
        CancellationToken cancellationToken = default)
    {
        SearchCalls++;

        query = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)>(
                (null, new ErrorInfo("query must not be empty")));
        }

        var results = _fixtures.Tickets.Values
            .Select(ticket => new
            {
                Ticket = ticket,
                HasTitleHit = Contains(ticket.Title, query),
                HasDescriptionHit = Contains(ticket.DescriptionText, query),
                ChangedDate = ParseChangedDate(ticket.ChangedDate),
                IsClosed = IsClosed(ticket.State)
            })
            .Where(x => x.HasTitleHit || (includeDescription && x.HasDescriptionHit))
            .Where(x => includeClosed || !x.IsClosed)
            .OrderBy(x => x.IsClosed)
            .ThenByDescending(x => x.HasTitleHit)
            .ThenByDescending(x => x.HasDescriptionHit)
            .ThenByDescending(x => x.ChangedDate)
            .Take(top)
            .Select(x => x.Ticket.ToSearchResult())
            .ToArray();

        return Task.FromResult<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)>((results, null));
    }

    private static bool Contains(string? value, string query)
        => !string.IsNullOrWhiteSpace(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset ParseChangedDate(string? value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;

    private static bool IsClosed(string? state)
        => !string.IsNullOrWhiteSpace(state) && ClosedStates.Contains(state);
}

internal sealed class FixtureBackedPullRequestClient(FixtureData? fixtures = null) : IAzureDevOpsPullRequestClient
{
    private readonly FixtureData _fixtures = fixtures ?? FixtureCatalog.Load();

    public int Calls { get; private set; }

    public Task<(PullRequestInfo? PullRequest, ErrorInfo? Error)> ReadPullRequestAsync(
        AzureDevOpsConnectionInfo connection,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default)
    {
        Calls++;

        var match = _fixtures.PullRequests.Values.FirstOrDefault(pr => pr.Matches(projectId, repositoryId, pullRequestId));
        return Task.FromResult<(PullRequestInfo? PullRequest, ErrorInfo? Error)>(
            match is not null
                ? (match.ToPullRequest(), null)
                : (null, new ErrorInfo("pull request not found")));
    }
}

internal sealed class ThrowingWorkItemClient : IAzureDevOpsWorkItemClient
{
    public int ReadCalls { get; private set; }

    public int SearchCalls { get; private set; }

    public Task<(Ticket? Ticket, ErrorInfo? Error)> ReadWorkItemAsync(
        AzureDevOpsConnectionInfo connection,
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        ReadCalls++;
        throw new InvalidOperationException("Client should not be called when configuration is missing.");
    }

    public Task<(IReadOnlyList<SearchTicketResult>? Results, ErrorInfo? Error)> SearchWorkItemsAsync(
        AzureDevOpsConnectionInfo connection,
        string query,
        int top = 20,
        bool includeClosed = false,
        bool includeDescription = false,
        CancellationToken cancellationToken = default)
    {
        SearchCalls++;
        throw new InvalidOperationException("Client should not be called when configuration is missing.");
    }
}

internal sealed class ThrowingPullRequestClient : IAzureDevOpsPullRequestClient
{
    public int Calls { get; private set; }

    public Task<(PullRequestInfo? PullRequest, ErrorInfo? Error)> ReadPullRequestAsync(
        AzureDevOpsConnectionInfo connection,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        throw new InvalidOperationException("Pull request client should not be called for this test.");
    }
}

internal sealed class ConfiguredConnectionState : IAzureDevOpsConnectionState
{
    public bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out ErrorInfo? error)
    {
        connection = new AzureDevOpsConnectionInfo("https://dev.azure.com/test-org", "pat", null);
        error = null;
        return true;
    }

    public string ConfigPath => "/tmp/azuremcp.json";
}

internal sealed class MissingConnectionState : IAzureDevOpsConnectionState
{
    public bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out ErrorInfo? error)
    {
        connection = default!;
        error = AzureMcpErrors.MissingConfig(ConfigPath, ["organizationUrl", "personalAccessToken"]);
        return false;
    }

    public string ConfigPath => "/tmp/azuremcp.json";
}

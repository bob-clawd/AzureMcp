![AzureMcp icon](assets/icon.png)

# AzureMcp

Small personal MCP server for the parts of Azure DevOps that are actually useful in day-to-day work.

This is intentionally not a full Azure or Azure DevOps surface area wrapper.
The goal is a small, composable toolbelt with a familiar architecture:

- `AzureMcp.Host` for MCP hosting and startup/config parsing
- `AzureMcp.Tools` for tool implementations and Azure DevOps integration
- `AzureMcp.Tools.Tests` for fast unit tests around parsing, options, and tool behavior

## First tools

The current tools are `read_work_item`, `get_context`, and `search_work_items`.

`read_work_item` accepts a work item id and returns a structured object with the fields that matter first:

- id
- title
- descriptionText (plain text)
- assignedTo
- state
- work item type
- parentId (when present)
- childrenIds
- branches
- pullRequestIds

That gives us a real end-to-end slice to shape the architecture before adding more tools.

`get_context` accepts any work item id, walks upward to the topmost parent, then returns the parent chain context in stable order from parent to child.

`search_work_items` accepts a free-text query and returns a compact ticket list with:

- id
- title
- state
- work item type
- changed date

By default it searches title only, excludes closed/done/removed items, and sorts open items first and then by most recently changed.

## Configuration

AzureMcp requires a config file path on startup.
The **config file is the source of truth** for the Azure DevOps connection.

If required values are missing when you call a tool, the server returns an actionable error:
ask the user for the missing value(s), then update the config file.

### Required

- `--config <path>` (required)

### Config file shape

```json
{
  "organizationUrl": "https://dev.azure.com/your-org",
  "personalAccessToken": "your-pat",
  "project": "optional-project"
}
```

## Run locally

```bash
export PATH="$PATH:/home/bob/.dotnet"
dotnet run -c Release --project src/AzureMcp.Host/AzureMcp.Host.csproj -- --config ~/.config/azuremcp/config.json
```

## Tool: `read_work_item`

Loads a single Azure DevOps work item and returns a structured view of the fields that matter first.

### Input

```json
{
  "workItemId": 12345
}
```

### Output

```json
{
  "ticket": {
    "id": 12345,
    "title": "Improve deployment diagnostics",
    "state": "Active",
    "workItemType": "User Story",
    "descriptionText": "Investigate missing logs during deployment.",
    "assignedTo": "Ada Lovelace",
    "parentId": 100,
    "childrenIds": [200, 201],
    "branches": ["feature/ado-12345"],
    "pullRequests": [
      {
        "id": 33,
        "descriptionText": "Tighten deployment log collection and retention checks."
      }
    ]
  },
  "error": null
}
```

## Tool: `get_context`

Walks upward from any work item id to the topmost parent and returns the chain ordered from parent to child.

### Input

```json
{
  "workItemId": 12345
}
```

### Output

```json
{
  "tickets": [
    {
      "id": 100,
      "title": "Parent feature",
      "workItemType": "Feature",
      "descriptionText": "High-level context"
    },
    {
      "id": 12345,
      "title": "Bug in child item",
      "workItemType": "Bug",
      "descriptionText": "Concrete problem"
    }
  ],
  "error": null
}
```

## Tool: `search_work_items`

Searches Azure DevOps work items by free-text query.
By default it searches title only, excludes closed/done/removed items, and sorts open items first and then by most recently changed.

### Input

```json
{
  "query": "deployment",
  "top": 20,
  "includeClosed": false,
  "includeDescription": false
}
```

### Output

```json
{
  "tickets": [
    {
      "id": 12345,
      "title": "Improve deployment diagnostics",
      "state": "Active",
      "workItemType": "User Story",
      "changedDate": "2026-04-26T07:00:00Z"
    },
    {
      "id": 12312,
      "title": "Deployment docs cleanup",
      "state": "New",
      "workItemType": "Task",
      "changedDate": "2026-04-25T18:30:00Z"
    }
  ],
  "error": null
}
```

## Next likely tools

Once this slice feels right, obvious next candidates are:

- read multiple work items
- search work items by query
- list pull requests
- read pull request
- list pipeline runs / get build status

But only if they earn their place.

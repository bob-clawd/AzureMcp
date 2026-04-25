![AzureMcp icon](assets/icon.png)

# AzureMcp

Small personal MCP server for the parts of Azure DevOps that are actually useful in day-to-day work.

This is intentionally not a full Azure or Azure DevOps surface area wrapper.
The goal is a small, composable toolbelt with a familiar architecture:

- `AzureMcp.Host` for MCP hosting and startup/config parsing
- `AzureMcp.Tools` for tool implementations and Azure DevOps integration
- `AzureMcp.Tools.Tests` for fast unit tests around parsing, options, and tool behavior

## First vertical slice

The first tool is `read_work_item`.

It accepts a work item id and returns a structured object with the fields that matter first:

- id
- title
- description (plain text + raw html when present)
- assigned person
- state
- work item type
- url
- parent work item id (when present)
- child work item ids
- related work item ids

That gives us a real end-to-end slice to shape the architecture before adding more tools.

## Configuration

AzureMcp requires a config file path on startup.
The **config file is the source of truth** for the Azure DevOps connection.

If required values are missing when you call a tool, the server returns an actionable error:
ask the user for the missing value(s), then call `configure_connection` to write the config file.

### Required

- `--config <path>` (required)

### Tool: `configure_connection`

You can set/update the connection values by writing/updating the config file via:

- `configure_connection(organizationUrl?, personalAccessToken?, project?)`

## Run locally

```bash
export PATH="$PATH:/home/bob/.dotnet"
dotnet run -c Release --project src/AzureMcp.Host/AzureMcp.Host.csproj -- --config ~/.config/azuremcp/config.json
```

## Tool: `read_work_item`

Input:

```json
{
  "workItemId": 12345
}
```

Example response shape:

```json
{
  "id": 12345,
  "title": "Improve deployment diagnostics",
  "state": "Active",
  "workItemType": "User Story",
  "descriptionText": "Investigate missing logs during deployment.",
  "descriptionHtml": "<div>Investigate missing logs during deployment.</div>",
  "assignedTo": {
    "displayName": "Ada Lovelace",
    "uniqueName": "ada@example.com"
  },
  "parentWorkItemId": 100,
  "childWorkItemIds": [200, 201],
  "relatedWorkItemIds": [300],
  "url": "https://dev.azure.com/your-org/_apis/wit/workItems/12345"
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

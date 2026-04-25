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

AzureMcp reads its Azure DevOps connection settings from command-line arguments or environment variables.
Command-line arguments win.

### Required

- `--organization-url` or `AZURE_MCP_ORGANIZATION_URL`
- `--pat` or `AZURE_MCP_PAT`

### Optional

- `--project` or `AZURE_MCP_PROJECT`

`project` is already part of the server configuration because it will likely matter for later tools, even though `read_work_item` itself only needs the organization and PAT.

## Run locally

```bash
export AZURE_MCP_ORGANIZATION_URL="https://dev.azure.com/your-org"
export AZURE_MCP_PAT="your-pat"
export AZURE_MCP_PROJECT="your-project" # optional

export PATH="$PATH:/home/bob/.dotnet"
dotnet run -c Release --project src/AzureMcp.Host/AzureMcp.Host.csproj
```

Or with explicit arguments:

```bash
export PATH="$PATH:/home/bob/.dotnet"
dotnet run -c Release --project src/AzureMcp.Host/AzureMcp.Host.csproj -- \
  --organization-url "https://dev.azure.com/your-org" \
  --pat "your-pat" \
  --project "your-project"
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

# AzureMcp Architecture

## Goal

AzureMcp is a small personal MCP server for Azure DevOps workflows that actually matter in daily work.

The architecture is intentionally minimal:

- easy to extend tool-by-tool
- easy to test without live Azure access
- easy to migrate later to Moldi's GitHub account

## Project layout

### `src/AzureMcp.Host`

Responsibilities:

- process startup
- MCP host wiring
- argument parsing
- dependency composition

This project should stay thin.
It should not contain Azure DevOps business logic.

### `src/AzureMcp.Tools`

Responsibilities:

- MCP tool classes
- Azure DevOps client abstractions and implementations
- DTOs / response models
- tool registration infrastructure

This is the real application layer.
Every new MCP capability should usually land here.

### `tests/AzureMcp.Tools.Tests`

Responsibilities:

- option parsing tests
- tool behavior tests
- Azure DevOps response parsing tests

Tests should prefer fakes/stubs over live Azure calls.
That keeps the suite fast and deterministic.

## Current vertical slices: `read_work_item` + `get_context`

`read_work_item` flow:

1. MCP client calls `read_work_item(id)`
2. `ReadWorkItemTool` delegates to `IAzureDevOpsWorkItemClient`
3. `AzureDevOpsWorkItemClient` calls Azure DevOps REST API
4. response is normalized into a compact structured model
5. MCP returns that model to the caller

`get_context` builds on the same client, but changes the retrieval shape:

1. MCP client calls `get_context(id)`
2. tool resolves the current item
3. tool walks upward to the topmost parent
4. tool traverses downward through child links
5. MCP returns one stable parent-to-children context list

These slices establish the default pattern for future tools:

- thin MCP tool
- dedicated integration client
- typed response model
- unit tests around parsing + mapping

## Configuration

AzureMcp resolves connection settings from:

1. built-in defaults
2. optional config file overrides (`--config <path>`)

Current intent:

- zero-config startup for self-hosted Azure DevOps Server
- optional config file for overrides
- PAT optional
- Windows Integrated Auth available without config
- PAT tried first when present, with Windows fallback on `401`/`403`

Malformed config files still fail fast.

## Extension strategy

When adding new tools, prefer this order:

1. add/extend a typed client capability
2. add a focused MCP tool around it
3. add tests for parsing + tool mapping
4. only then expose more surface area

Do not add broad generic wrappers around the whole Azure DevOps API unless a real personal workflow needs them.

## Non-goals for now

- full Azure support
- full Azure DevOps API coverage
- write/mutation tools before read paths are proven useful
- giant configuration model too early

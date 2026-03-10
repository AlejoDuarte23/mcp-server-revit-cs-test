---
name: revit-mcp-tool
description: Add or update a tool in the Revit MCP demo repo. Use when creating new bridge tools, request/response DTOs, Revit handlers, or the external server surface for this project.
---

# Revit MCP Tool

Use this skill when extending `/Users/alejandroduarte/Documents/revit-mcp-cs-test` with a new tool.

## Goal

Add a new tool without breaking the execution boundary:

- `RevitMcp.Server` exposes the tool
- `BridgeClient` forwards the request
- `RevitMcp.RevitAddin` executes the Revit API code
- Revit API calls stay inside Revit and run through `ExternalEvent`

## Non-negotiable rules

1. Do not call the Revit API from `RevitMcp.Server`.
2. Do not call the Revit API directly from the HTTP listener thread.
3. Keep Revit work in handlers under `src/RevitMcp.RevitAddin/Handlers/`.
4. Use `RevitMcp.Core` for reusable Revit-facing service logic.
5. For write operations, use a Revit `Transaction`.
6. Keep `RevitMcp.Contracts` JSON-safe and cross-target compatible.

## Existing flow

1. Add-in startup wires handlers in `src/RevitMcp.RevitAddin/App.cs`.
2. `BridgeRequestBroker` raises an `ExternalEvent`.
3. `CommandDispatcher` looks up the handler by tool name.
4. The handler executes and returns a serializable result.
5. `RevitMcp.Server/Tools/RevitTools.cs` exposes a thin method that calls `BridgeClient`.
6. `RevitMcp.Server/Program.cs` exposes the current demo HTTP endpoint.

## Standard workflow for a new tool

### Read-only tool

1. Decide the tool name in snake case, for example `list_levels`.
2. If the tool needs typed arguments or typed shared payloads, add them under `src/RevitMcp.Contracts/`.
3. Add or extend a service under `src/RevitMcp.Core/Services/` for the Revit-side logic.
4. Add a handler under `src/RevitMcp.RevitAddin/Handlers/`.
5. Register the handler in `src/RevitMcp.RevitAddin/App.cs`.
6. Add a thin method in `src/RevitMcp.Server/Tools/RevitTools.cs`.
7. Add an endpoint in `src/RevitMcp.Server/Program.cs`.
8. Update `README.md` if the public tool list changed.

### Write tool

Follow the same flow, plus:

1. Validate the active document and input arguments early.
2. Wrap model changes in `using (var tx = new Transaction(doc, "..."))`.
3. Keep the transaction scope small.
4. Return a compact success payload with the created or modified element identifiers.

## Handler template

```csharp
using Autodesk.Revit.UI;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class ListLevelsHandler : IRevitCommandHandler
{
    public string Name => "list_levels";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var service = new LevelService();
        return service.ListLevels(doc);
    }
}
```

## Server method template

```csharp
public async Task<object> ListLevels(CancellationToken cancellationToken = default)
{
    var response = await _bridge.InvokeAsync("list_levels", new { }, cancellationToken);
    if (!response.Success)
    {
        throw new InvalidOperationException(response.Error);
    }

    return response.Result!;
}
```

## HTTP endpoint template

```csharp
app.MapGet("/tools/list_levels", async (RevitTools tools, CancellationToken ct) =>
    Results.Ok(await tools.ListLevels(ct)));
```

## Where to edit

- Shared DTOs: `src/RevitMcp.Contracts/`
- Revit logic: `src/RevitMcp.Core/Services/`
- Handler registration: `src/RevitMcp.RevitAddin/App.cs`
- Revit handlers: `src/RevitMcp.RevitAddin/Handlers/`
- Bridge client: `src/RevitMcp.Server/BridgeClient.cs`
- Tool surface: `src/RevitMcp.Server/Tools/RevitTools.cs`
- Demo endpoints: `src/RevitMcp.Server/Program.cs`

## Versioning and targeting notes

- `RevitVersion=2024` builds Revit projects for `net48`.
- `RevitVersion=2025` and `2026` build Revit projects for `net8.0-windows`.
- `RevitMcp.Contracts` targets `netstandard2.0`, so avoid APIs that are unavailable there unless you add compatible packages.

## Build and registration commands

```powershell
.\scripts\build-all.ps1 -RevitVersion 2024 -Configuration Debug
.\scripts\build-all.ps1 -RevitVersion 2026 -Configuration Debug -RegisterAddin
.\scripts\register-addin.ps1 -RevitVersion 2024 -Configuration Debug -Scope CurrentUser
```

## Validation checklist

- Handler is registered in `App.cs`
- Tool name matches in handler, server method, and endpoint
- Result is serializable
- No Revit API usage leaked into `RevitMcp.Server`
- Write operations use `Transaction`
- `README.md` reflects new externally visible tools


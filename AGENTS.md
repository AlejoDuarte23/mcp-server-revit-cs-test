# AGENTS.md

## Purpose

This repository is a demo bridge between Autodesk Revit and an external tool surface.

- `RevitMcp.RevitAddin` runs inside Revit.
- `RevitMcp.Core` contains Revit-facing logic that depends on `Autodesk.Revit.DB`.
- `RevitMcp.Contracts` contains JSON-safe DTOs shared across the bridge.
- `RevitMcp.Server` stays outside Revit and must not reference the Revit API.

## Non-Negotiable Architecture Rules

1. Never call the Revit API directly from the HTTP listener or any background thread.
2. All Revit API execution must be marshaled through Revit-controlled execution, currently `ExternalEvent` via `BridgeRequestBroker`.
3. Keep `RevitMcp.Server` and `RevitMcp.Contracts` free of `RevitAPI.dll` or `RevitAPIUI.dll` references.
4. Keep bridge payloads serializable with `System.Text.Json`.
5. Any write operation must run inside a Revit `Transaction`.

## Build Rules

- Target supported Revit versions with `/p:RevitVersion=2025` or `/p:RevitVersion=2026`.
- If Revit is installed in a custom location, build with `/p:RevitInstallDir="C:\Path\To\Revit"`.
- Required Revit references for the add-in are:
  - `RevitAPI.dll`
  - `RevitAPIUI.dll`
- Required Revit reference for `RevitMcp.Core` is:
  - `RevitAPI.dll`

## Registration Rules

- The add-in manifest must be copied to a Revit add-ins folder that matches the target version.
- Per-user registration:
  - `%AppData%\Autodesk\Revit\Addins\2025\`
  - `%AppData%\Autodesk\Revit\Addins\2026\`
- All-users registration:
  - `%ProgramData%\Autodesk\Revit\Addins\2025\`
  - `%ProgramData%\Autodesk\Revit\Addins\2026\`
- The manifest `Assembly` path must point to the built `RevitMcp.RevitAddin.dll`.

## When Extending The Tool Set

- Add shared request/response models under `RevitMcp.Contracts` when arguments stop being trivial.
- Add a handler under `RevitMcp.RevitAddin/Handlers`.
- Keep the server surface thin; it should only validate input and forward calls through `BridgeClient`.
- Update `README.md` when endpoints, ports, manifest instructions, or prerequisites change.

## Current Demo Boundary

- The current `RevitMcp.Server` project is an HTTP demo server, not a production MCP stdio server yet.
- If you replace it with a real MCP transport later, preserve the existing boundary:
  - external server
  - local bridge
  - Revit add-in


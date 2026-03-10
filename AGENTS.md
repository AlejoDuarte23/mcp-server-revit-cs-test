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

- Target supported Revit versions with `/p:RevitVersion=2024`, `/p:RevitVersion=2025`, or `/p:RevitVersion=2026`.
- Revit 2024 builds against .NET Framework 4.8; Revit 2025+ builds against .NET 8.
- The project files automatically select the correct target framework based on `RevitVersion`.
- If Revit is installed in a custom location, build with `/p:RevitInstallDir="C:\Path\To\Revit"`.
- Required Revit references for the add-in are:
  - `RevitAPI.dll`
  - `RevitAPIUI.dll`
- Required Revit reference for `RevitMcp.Core` is:
  - `RevitAPI.dll`

## Registration Rules

- The add-in manifest must be copied to a Revit add-ins folder that matches the target version.
- Per-user registration:
  - `%AppData%\Autodesk\Revit\Addins\2024\`
  - `%AppData%\Autodesk\Revit\Addins\2025\`
  - `%AppData%\Autodesk\Revit\Addins\2026\`
- All-users registration:
  - `%ProgramData%\Autodesk\Revit\Addins\2024\`
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

## Learnings

### .NET Framework and Target Compatibility

1. **netstandard2.0 for Contracts**
   - `RevitMcp.Contracts` targets `netstandard2.0` for cross-platform compatibility
   - This allows it to be consumed by both .NET Framework 4.8 (Revit 2024) and .NET 8+ (Revit 2025+, Server)

2. **System.Text.Json Package Required**
   - `System.Text.Json` is NOT included in `netstandard2.0` by default
   - Must be added explicitly as a NuGet `PackageReference` in `RevitMcp.Contracts.csproj`
   - Use version `8.0.5` or later to avoid known security vulnerabilities (CVE-2024-*)
   - Earlier versions (8.0.0) have high-severity vulnerabilities

3. **IsExternalInit Polyfill for Records**
   - C# 9 record types require `System.Runtime.CompilerServices.IsExternalInit`
   - This type is missing in `netstandard2.0`
   - Solution: Add a polyfill file `IsExternalInit.cs` to `RevitMcp.Contracts`:
     ```csharp
     namespace System.Runtime.CompilerServices;
     internal static class IsExternalInit { }
     ```

4. **Server .NET Version Flexibility**
   - `RevitMcp.Server` can target any modern .NET version (net8.0, net10.0, etc.)
   - It runs outside Revit and doesn't depend on Revit's runtime
   - If building on a machine with .NET 10+ only, update server to `net10.0`

### Revit API Compatibility

5. **ElementId.Value vs IntegerValue**
   - `ElementId.IntegerValue` is deprecated starting in Revit 2024
   - Use `ElementId.Value` for all Revit versions (2024+)
   - `Value` returns `long` instead of `int`
   - No conditional compilation needed - just use `.Value` everywhere

6. **Manifest File Required**
   - The `.addin` manifest file must exist at `src/RevitMcp.RevitAddin/Manifest/RevitMcp.RevitAddin.addin`
   - The registration scripts expect this file and will patch the `<Assembly>` path automatically
   - Manifest structure:
     ```xml
     <RevitAddIns>
       <AddIn Type="Application">
         <Name>RevitMcp</Name>
         <Assembly>PLACEHOLDER</Assembly>
         <FullClassName>RevitMcp.RevitAddin.App</FullClassName>
         <ClientId>A1B2C3D4-E5F6-4A5B-8C9D-1E2F3A4B5C6D</ClientId>
         <VendorId>RVTMCP</VendorId>
       </AddIn>
     </RevitAddIns>
     ```

### Build Process

7. **Multi-Targeting Strategy**
   - Use conditional `<TargetFramework>` in `.csproj` files based on `$(RevitVersion)` MSBuild property
   - Revit 2024: `net48`
   - Revit 2025+: `net8.0-windows`
   - Build scripts handle the correct output paths automatically

8. **Build Order Matters**
   - Always build the solution (not individual projects) to ensure correct dependency resolution
   - `RevitMcp.Contracts` → `RevitMcp.Core` → `RevitMcp.RevitAddin`
   - `RevitMcp.Server` can build independently

### Testing Workflow

9. **Startup Sequence**
   - Start Revit first and open a project
   - The add-in starts its bridge on `http://127.0.0.1:5057/` after Revit initializes
   - Then start the demo server on `http://127.0.0.1:5099`
   - Test with curl: `ping`, `get_active_document`, `list_walls`

10. **Troubleshooting**
    - Check Revit Journal files at `%AppData%\Autodesk\Revit\{version}\Journals\` for add-in load errors
    - If `HttpListener` access denied: run `netsh http add urlacl url=http://127.0.0.1:5057/ user=%USERNAME%` in elevated PowerShell
    - Verify manifest registered in correct Revit version folder


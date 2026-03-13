using ModelContextProtocol.Server;
using System.ComponentModel;

namespace RevitMcp.Server.Tools;

[McpServerToolType]
public class RevitTools
{
    private readonly BridgeClient _bridge;

    public RevitTools(BridgeClient bridge)
    {
        _bridge = bridge;
    }
    [McpServerTool(Name = "ping", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Check whether the Revit bridge is reachable and return the Revit product version.")]
    public async Task<object> Ping(CancellationToken cancellationToken = default)
    {
        var response = await _bridge.InvokeAsync("ping", new { }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }

    [McpServerTool(Name = "get_active_document", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Get metadata about the active Revit document in the current desktop session.")]
    public async Task<object> GetActiveDocument(CancellationToken cancellationToken = default)
    {
        var response = await _bridge.InvokeAsync("get_active_document", new { }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }

    [McpServerTool(Name = "list_walls", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List wall elements from the active Revit document and return their identifiers, names, and categories.")]
    public async Task<object> ListWalls(CancellationToken cancellationToken = default)
    {
        var response = await _bridge.InvokeAsync("list_walls", new { }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }

    [McpServerTool(Name = "colorize_duct_pressure_drop", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Color ducts, flex ducts, and duct fittings in the active view by pressure drop (cold-to-warm gradient) and return the top pressure-drop elements with full properties.")]
    public async Task<object> ColorizeDuctPressureDrop(
        [Description("How many top pressure-drop elements to return. Default is 10.")]
        int topCount = 10,
        CancellationToken cancellationToken = default)
    {
        var response = await _bridge.InvokeAsync("colorize_duct_pressure_drop", new { topCount }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }

    [McpServerTool(Name = "extract_system_air_elements", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Extract ducts, flex ducts, air terminals, and duct fittings for a given System Name, color them blue in the active view, and save a compact HVAC-rule JSON schema to local disk.")]
    public async Task<object> ExtractSystemAirElements(
        [Description("Exact System Name to filter by, e.g. 'Mechanical Supply Air 17'.")]
        string systemName,
        [Description("Optional output directory path. If omitted, uses REVIT_MCP_EXPORT_DIR or LocalAppData fallback.")]
        string? outputDir = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _bridge.InvokeAsync("extract_system_air_elements", new { systemName, outputDir }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }

    [McpServerTool(Name = "check_zero_pressure_drop_fittings", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Find duct fittings in a given system with zero pressure drop, color those fittings in the active view, and return their element IDs.")]
    public async Task<object> CheckZeroPressureDropFittings(
        [Description("Exact System Name to filter by, e.g. 'Mechanical Supply Air 17'.")]
        string systemName,
        [Description("Tolerance used when checking whether pressure drop is zero. Default is 1e-9.")]
        double? zeroTolerance = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _bridge.InvokeAsync("check_zero_pressure_drop_fittings", new { systemName, zeroTolerance }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }

    [McpServerTool(Name = "set_fitting_specific_coefficient", ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false)]
    [Description("Set duct fitting loss method to Specific Coefficient and set coefficient value (default 0.2) for fittings in a given system.")]
    public async Task<object> SetFittingSpecificCoefficient(
        [Description("Exact System Name to filter by, e.g. 'Mechanical Supply Air 17'.")]
        string systemName,
        [Description("Specific coefficient value to set. Default is 0.2.")]
        double coefficient = 0.2,
        [Description("When true (default), apply changes only to zero-pressure fittings in the system.")]
        bool onlyZeroPressure = true,
        [Description("Tolerance used when checking whether pressure drop is zero. Default is 1e-9.")]
        double? zeroTolerance = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _bridge.InvokeAsync(
            "set_fitting_specific_coefficient",
            new { systemName, coefficient, onlyZeroPressure, zeroTolerance },
            cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }
}

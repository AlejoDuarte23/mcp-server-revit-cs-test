using ModelContextProtocol.Server;
using RevitMcp.Contracts;
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

    [McpServerTool(Name = "color_elements_by_id", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Apply a caller-provided RGB color override to specific Revit elements in the active view using their Revit element IDs.")]
    public async Task<object> ColorElementsById(
        [Description("Revit element IDs to color in the active view.")]
        List<long> elementIds,
        [Description("Red channel value from 0 to 255.")]
        byte red,
        [Description("Green channel value from 0 to 255.")]
        byte green,
        [Description("Blue channel value from 0 to 255.")]
        byte blue,
        CancellationToken cancellationToken = default)
    {
        var request = new ColorElementsByIdRequest
        {
            ElementIds = elementIds ?? new List<long>(),
            Red = red,
            Green = green,
            Blue = blue
        };

        var response = await _bridge.InvokeAsync("color_elements_by_id", request, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }

    [McpServerTool(Name = "get_low_velocity_duct_elements", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return ducts and flex ducts whose velocity is strictly lower than the provided threshold in meters per second.")]
    public async Task<object> GetLowVelocityDuctElements(
        [Description("Maximum allowed velocity in meters per second.")]
        double maxVelocityMetersPerSecond,
        CancellationToken cancellationToken = default)
    {
        var request = new GetLowVelocityDuctElementsRequest
        {
            MaxVelocityMetersPerSecond = maxVelocityMetersPerSecond
        };

        var response = await _bridge.InvokeAsync("get_low_velocity_duct_elements", request, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }

    [McpServerTool(Name = "set_duct_dimensions", ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false)]
    [Description("Set width and/or height in millimeters for duct elements identified by Revit element ID.")]
    public async Task<object> SetDuctDimensions(
        [Description("Revit element IDs for duct elements to update.")]
        List<long> elementIds,
        [Description("Optional width in millimeters. Provide null to leave width unchanged.")]
        double? widthMillimeters = null,
        [Description("Optional height in millimeters. Provide null to leave height unchanged.")]
        double? heightMillimeters = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SetDuctDimensionsRequest
        {
            ElementIds = elementIds ?? new List<long>(),
            WidthMillimeters = widthMillimeters,
            HeightMillimeters = heightMillimeters
        };

        var response = await _bridge.InvokeAsync("set_duct_dimensions", request, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result!;
    }

    [McpServerTool(Name = "set_flex_duct_diameter", ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false)]
    [Description("Set diameter in millimeters for flex duct elements identified by Revit element ID.")]
    public async Task<object> SetFlexDuctDiameter(
        [Description("Revit element IDs for flex duct elements to update.")]
        List<long> elementIds,
        [Description("Diameter in millimeters.")]
        double diameterMillimeters,
        CancellationToken cancellationToken = default)
    {
        var request = new SetFlexDuctDiameterRequest
        {
            ElementIds = elementIds ?? new List<long>(),
            DiameterMillimeters = diameterMillimeters
        };

        var response = await _bridge.InvokeAsync("set_flex_duct_diameter", request, cancellationToken);
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

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
}

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
}

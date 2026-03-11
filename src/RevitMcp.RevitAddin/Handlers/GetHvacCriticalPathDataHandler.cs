using Autodesk.Revit.UI;
using RevitMcp.Contracts;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class GetHvacCriticalPathDataHandler : IRevitCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "get_hvac_critical_path_data";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var request = JsonSerializer.Deserialize<GetHvacCriticalPathDataRequest>(args.GetRawText(), JsonOptions)
            ?? throw new InvalidOperationException("Invalid request payload.");

        var service = new HvacCriticalPathService();
        return service.GetCriticalPathData(doc, request);
    }
}

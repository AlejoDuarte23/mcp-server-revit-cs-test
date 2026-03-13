using Autodesk.Revit.UI;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class ColorizeDuctPressureDropHandler : IRevitCommandHandler
{
    public string Name => "colorize_duct_pressure_drop";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var topCount = 10;
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty("topCount", out var topCountElement) &&
            topCountElement.TryGetInt32(out var parsedTopCount))
        {
            topCount = parsedTopCount;
        }

        var service = new DuctPressureDropService();
        return service.ColorizeAndGetTopPressureDrop(doc, topCount);
    }
}

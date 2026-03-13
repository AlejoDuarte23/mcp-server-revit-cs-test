using Autodesk.Revit.UI;
using RevitMcp.Contracts;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class ColorElementsByIdHandler : IRevitCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "color_elements_by_id";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var request = JsonSerializer.Deserialize<ColorElementsByIdRequest>(args.GetRawText(), JsonOptions)
            ?? throw new ArgumentException("Invalid request payload.");

        if (request.ElementIds.Count == 0)
        {
            throw new ArgumentException("'elementIds' is required.");
        }

        var service = new ElementColorOverrideService();
        var result = service.ApplyColor(doc, request.ElementIds, request.Red, request.Green, request.Blue);

        return new ColorElementsByIdResult
        {
            ViewId = result.ViewId,
            ViewName = result.ViewName,
            Red = result.Red,
            Green = result.Green,
            Blue = result.Blue,
            RequestedElementIds = result.RequestedElementIds,
            AppliedElementIds = result.AppliedElementIds,
            MissingElementIds = result.MissingElementIds,
            FailedElementIds = result.FailedElementIds,
            Message = result.Message
        };
    }
}

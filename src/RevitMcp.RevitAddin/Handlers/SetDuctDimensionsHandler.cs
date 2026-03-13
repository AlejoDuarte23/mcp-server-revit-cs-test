using Autodesk.Revit.UI;
using RevitMcp.Contracts;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class SetDuctDimensionsHandler : IRevitCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "set_duct_dimensions";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var request = JsonSerializer.Deserialize<SetDuctDimensionsRequest>(args.GetRawText(), JsonOptions)
            ?? throw new ArgumentException("Invalid request payload.");

        if (request.ElementIds.Count == 0)
        {
            throw new ArgumentException("'elementIds' is required.");
        }

        var service = new DuctToolService();
        var result = service.SetDuctDimensions(doc, request.ElementIds, request.WidthMillimeters, request.HeightMillimeters);

        return new SetDuctDimensionsResult
        {
            WidthMillimeters = result.WidthMillimeters,
            HeightMillimeters = result.HeightMillimeters,
            RequestedElementIds = result.RequestedElementIds,
            UpdatedElementIds = result.UpdatedElementIds,
            MissingElementIds = result.MissingElementIds,
            FailedElementIds = result.FailedElementIds,
            Message = result.Message
        };
    }
}

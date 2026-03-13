using Autodesk.Revit.UI;
using RevitMcp.Contracts;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class SetFlexDuctDiameterHandler : IRevitCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "set_flex_duct_diameter";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var request = JsonSerializer.Deserialize<SetFlexDuctDiameterRequest>(args.GetRawText(), JsonOptions)
            ?? throw new ArgumentException("Invalid request payload.");

        if (request.ElementIds.Count == 0)
        {
            throw new ArgumentException("'elementIds' is required.");
        }

        var service = new DuctToolService();
        var result = service.SetFlexDuctDiameter(doc, request.ElementIds, request.DiameterMillimeters);

        return new SetFlexDuctDiameterResult
        {
            DiameterMillimeters = result.DiameterMillimeters,
            RequestedElementIds = result.RequestedElementIds,
            UpdatedElementIds = result.UpdatedElementIds,
            MissingElementIds = result.MissingElementIds,
            FailedElementIds = result.FailedElementIds,
            Message = result.Message
        };
    }
}

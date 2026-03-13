using Autodesk.Revit.UI;
using RevitMcp.Contracts;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class GetLowVelocityDuctElementsHandler : IRevitCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "get_low_velocity_duct_elements";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var request = JsonSerializer.Deserialize<GetLowVelocityDuctElementsRequest>(args.GetRawText(), JsonOptions)
            ?? throw new ArgumentException("Invalid request payload.");

        var service = new DuctToolService();
        var result = service.GetLowVelocityElements(doc, request.MaxVelocityMetersPerSecond);

        return new GetLowVelocityDuctElementsResult
        {
            MaxVelocityMetersPerSecond = result.MaxVelocityMetersPerSecond,
            ScannedElements = result.ScannedElements,
            ElementsWithVelocity = result.ElementsWithVelocity,
            MatchingElements = result.MatchingElements,
            ElementIds = result.ElementIds,
            Items = result.Items
                .Select(x => new LowVelocityDuctElement
                {
                    ElementId = x.ElementId,
                    Category = x.Category,
                    Name = x.Name,
                    Size = x.Size,
                    VelocityMetersPerSecond = x.VelocityMetersPerSecond,
                    VelocityDisplay = x.VelocityDisplay
                })
                .ToList()
        };
    }
}

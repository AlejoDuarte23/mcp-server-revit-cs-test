using Autodesk.Revit.UI;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class CheckZeroPressureDropFittingsHandler : IRevitCommandHandler
{
    public string Name => "check_zero_pressure_drop_fittings";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var systemName = GetRequiredString(args, "systemName");
        var zeroTolerance = GetOptionalDouble(args, "zeroTolerance") ?? 1e-9;

        var service = new DuctFittingLossService();
        var result = service.ColorZeroPressureDropFittings(doc, systemName, zeroTolerance);

        return new
        {
            Message = $"Found {result.Summary.ZeroPressureFittings} zero-pressure fittings in system '{result.SystemName}'.",
            SystemName = result.SystemName,
            Summary = result.Summary,
            ElementIds = result.ElementIds
        };
    }

    private static string GetRequiredString(JsonElement args, string propertyName)
    {
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new ArgumentException($"'{propertyName}' is required.");
    }

    private static double? GetOptionalDouble(JsonElement args, string propertyName)
    {
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty(propertyName, out var property) &&
            property.TryGetDouble(out var value))
        {
            return value;
        }

        return null;
    }
}

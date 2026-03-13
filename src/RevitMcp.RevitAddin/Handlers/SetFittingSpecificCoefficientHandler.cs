using Autodesk.Revit.UI;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class SetFittingSpecificCoefficientHandler : IRevitCommandHandler
{
    public string Name => "set_fitting_specific_coefficient";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var systemName = GetRequiredString(args, "systemName");
        var coefficient = GetOptionalDouble(args, "coefficient") ?? 0.2;
        var onlyZeroPressure = GetOptionalBool(args, "onlyZeroPressure") ?? true;
        var zeroTolerance = GetOptionalDouble(args, "zeroTolerance") ?? 1e-9;

        var service = new DuctFittingLossService();
        var result = service.SetSpecificCoefficient(doc, systemName, coefficient, onlyZeroPressure, zeroTolerance);

        return new
        {
            Message =
                $"Updated {result.UpdatedElementIds.Count} fittings to Specific Coefficient ({result.Coefficient}) " +
                $"in system '{result.SystemName}'.",
            SystemName = result.SystemName,
            Coefficient = result.Coefficient,
            OnlyZeroPressure = result.OnlyZeroPressure,
            UpdatedElementIds = result.UpdatedElementIds,
            FailedElementIds = result.FailedElementIds
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

    private static bool? GetOptionalBool(JsonElement args, string propertyName)
    {
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty(propertyName, out var property) &&
            (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            return property.GetBoolean();
        }

        return null;
    }
}

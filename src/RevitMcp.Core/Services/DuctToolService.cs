using Autodesk.Revit.DB;
using System.Globalization;

namespace RevitMcp.Core.Services;

public sealed class DuctToolService
{
    public LowVelocityDuctElementsQueryResult GetLowVelocityElements(
        Document doc,
        double maxVelocityMetersPerSecond)
    {
        if (maxVelocityMetersPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxVelocityMetersPerSecond), "Velocity threshold must be non-negative.");
        }

        var categoryIds = new List<ElementId>
        {
            new(BuiltInCategory.OST_DuctCurves),
            new(BuiltInCategory.OST_FlexDuctCurves)
        };

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(categoryIds))
            .ToList();

        var items = new List<LowVelocityDuctElementResult>();
        var elementsWithVelocity = 0;

        foreach (var element in elements)
        {
            if (!TryGetVelocityMetersPerSecond(element, out var velocityMetersPerSecond, out var velocityDisplay))
            {
                continue;
            }

            elementsWithVelocity++;

            if (velocityMetersPerSecond >= maxVelocityMetersPerSecond)
            {
                continue;
            }

            items.Add(new LowVelocityDuctElementResult
            {
                ElementId = element.Id.Value,
                Category = element.Category?.Name,
                Name = element.Name,
                Size = ReadParameterText(FindParameter(element, BuiltInParameter.RBS_CALCULATED_SIZE, "Size")),
                VelocityMetersPerSecond = velocityMetersPerSecond,
                VelocityDisplay = velocityDisplay
            });
        }

        return new LowVelocityDuctElementsQueryResult
        {
            MaxVelocityMetersPerSecond = maxVelocityMetersPerSecond,
            ScannedElements = elements.Count,
            ElementsWithVelocity = elementsWithVelocity,
            MatchingElements = items.Count,
            ElementIds = items.Select(x => x.ElementId).ToList(),
            Items = items
        };
    }

    public DuctDimensionsUpdateResult SetDuctDimensions(
        Document doc,
        IEnumerable<long> elementIds,
        double? widthMillimeters,
        double? heightMillimeters)
    {
        if (elementIds is null)
        {
            throw new ArgumentNullException(nameof(elementIds));
        }

        if (widthMillimeters is null && heightMillimeters is null)
        {
            throw new ArgumentException("At least one of widthMillimeters or heightMillimeters is required.");
        }

        if (widthMillimeters is not null && widthMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthMillimeters), "Width must be greater than zero.");
        }

        if (heightMillimeters is not null && heightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightMillimeters), "Height must be greater than zero.");
        }

        var requestedElementIds = NormalizeElementIds(elementIds);
        var updatedElementIds = new List<long>();
        var missingElementIds = new List<long>();
        var failedElementIds = new List<long>();

        using var tx = new Transaction(doc, "Set Duct Dimensions");
        tx.Start();

        foreach (var elementIdValue in requestedElementIds)
        {
            var element = doc.GetElement(new ElementId(elementIdValue));
            if (element is null)
            {
                missingElementIds.Add(elementIdValue);
                continue;
            }

            if (element.Category?.Id.Value != (long)BuiltInCategory.OST_DuctCurves)
            {
                failedElementIds.Add(elementIdValue);
                continue;
            }

            try
            {
                var updated = true;

                if (widthMillimeters is not null)
                {
                    updated &= TrySetLengthParameter(element, BuiltInParameter.RBS_CURVE_WIDTH_PARAM, widthMillimeters.Value);
                }

                if (heightMillimeters is not null)
                {
                    updated &= TrySetLengthParameter(element, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM, heightMillimeters.Value);
                }

                if (updated)
                {
                    updatedElementIds.Add(elementIdValue);
                }
                else
                {
                    failedElementIds.Add(elementIdValue);
                }
            }
            catch
            {
                failedElementIds.Add(elementIdValue);
            }
        }

        tx.Commit();

        return new DuctDimensionsUpdateResult
        {
            WidthMillimeters = widthMillimeters,
            HeightMillimeters = heightMillimeters,
            RequestedElementIds = requestedElementIds,
            UpdatedElementIds = updatedElementIds,
            MissingElementIds = missingElementIds,
            FailedElementIds = failedElementIds,
            Message = $"Updated {updatedElementIds.Count} of {requestedElementIds.Count} requested duct elements."
        };
    }

    public FlexDuctDiameterUpdateResult SetFlexDuctDiameter(
        Document doc,
        IEnumerable<long> elementIds,
        double diameterMillimeters)
    {
        if (elementIds is null)
        {
            throw new ArgumentNullException(nameof(elementIds));
        }

        if (diameterMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(diameterMillimeters), "Diameter must be greater than zero.");
        }

        var requestedElementIds = NormalizeElementIds(elementIds);
        var updatedElementIds = new List<long>();
        var missingElementIds = new List<long>();
        var failedElementIds = new List<long>();

        using var tx = new Transaction(doc, "Set Flex Duct Diameter");
        tx.Start();

        foreach (var elementIdValue in requestedElementIds)
        {
            var element = doc.GetElement(new ElementId(elementIdValue));
            if (element is null)
            {
                missingElementIds.Add(elementIdValue);
                continue;
            }

            if (element.Category?.Id.Value != (long)BuiltInCategory.OST_FlexDuctCurves)
            {
                failedElementIds.Add(elementIdValue);
                continue;
            }

            try
            {
                if (TrySetLengthParameter(element, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM, diameterMillimeters))
                {
                    updatedElementIds.Add(elementIdValue);
                }
                else
                {
                    failedElementIds.Add(elementIdValue);
                }
            }
            catch
            {
                failedElementIds.Add(elementIdValue);
            }
        }

        tx.Commit();

        return new FlexDuctDiameterUpdateResult
        {
            DiameterMillimeters = diameterMillimeters,
            RequestedElementIds = requestedElementIds,
            UpdatedElementIds = updatedElementIds,
            MissingElementIds = missingElementIds,
            FailedElementIds = failedElementIds,
            Message = $"Updated {updatedElementIds.Count} of {requestedElementIds.Count} requested flex ducts."
        };
    }

    private static List<long> NormalizeElementIds(IEnumerable<long> elementIds)
    {
        var normalized = elementIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (normalized.Count == 0)
        {
            throw new ArgumentException("At least one positive element ID is required.", nameof(elementIds));
        }

        return normalized;
    }

    private static bool TryGetVelocityMetersPerSecond(
        Element element,
        out double velocityMetersPerSecond,
        out string? velocityDisplay)
    {
        var parameter = FindParameter(element, BuiltInParameter.RBS_VELOCITY, "Velocity");
        velocityDisplay = ReadParameterText(parameter);

        if (parameter is null)
        {
            velocityMetersPerSecond = 0;
            return false;
        }

        var internalValue = ReadParameterDouble(parameter);
        if (internalValue is null)
        {
            velocityMetersPerSecond = 0;
            return false;
        }

        velocityMetersPerSecond = ConvertFromInternalUnits(internalValue.Value, "MetersPerSecond");
        return !double.IsNaN(velocityMetersPerSecond) && !double.IsInfinity(velocityMetersPerSecond);
    }

    private static bool TrySetLengthParameter(Element element, BuiltInParameter builtInParameter, double valueMillimeters)
    {
        var parameter = element.get_Parameter(builtInParameter);
        if (parameter is null || parameter.IsReadOnly)
        {
            return false;
        }

        var internalValue = ConvertToInternalUnits(valueMillimeters, "Millimeters");

        switch (parameter.StorageType)
        {
            case StorageType.Double:
                return parameter.Set(internalValue);
            case StorageType.Integer:
                return parameter.Set((int)Math.Round(internalValue));
            case StorageType.String:
                return parameter.Set(valueMillimeters.ToString(CultureInfo.InvariantCulture));
            default:
                return parameter.SetValueString(valueMillimeters.ToString("G", CultureInfo.InvariantCulture));
        }
    }

    private static Parameter? FindParameter(Element element, BuiltInParameter builtInParameter, params string[] names)
    {
        var byBuiltIn = element.get_Parameter(builtInParameter);
        if (byBuiltIn is not null)
        {
            return byBuiltIn;
        }

        foreach (var name in names)
        {
            foreach (var parameter in element.Parameters.Cast<Parameter>())
            {
                if (string.Equals(parameter.Definition?.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return parameter;
                }
            }
        }

        return null;
    }

    private static string? ReadParameterText(Parameter? parameter)
    {
        if (parameter is null)
        {
            return null;
        }

        if (parameter.StorageType == StorageType.String)
        {
            return parameter.AsString();
        }

        try
        {
            return parameter.AsValueString();
        }
        catch
        {
            return null;
        }
    }

    private static double? ReadParameterDouble(Parameter parameter)
    {
        return parameter.StorageType switch
        {
            StorageType.Double => parameter.AsDouble(),
            StorageType.Integer => parameter.AsInteger(),
            _ => null
        };
    }

    private static double ConvertFromInternalUnits(double value, string unitTypeIdName)
    {
        try
        {
            var unitProperty = typeof(UnitTypeId).GetProperty(unitTypeIdName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (unitProperty?.GetValue(null) is ForgeTypeId unitTypeId)
            {
                return UnitUtils.ConvertFromInternalUnits(value, unitTypeId);
            }
        }
        catch
        {
            // Fall back to internal units if conversion fails.
        }

        return value;
    }

    private static double ConvertToInternalUnits(double value, string unitTypeIdName)
    {
        try
        {
            var unitProperty = typeof(UnitTypeId).GetProperty(unitTypeIdName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (unitProperty?.GetValue(null) is ForgeTypeId unitTypeId)
            {
                return UnitUtils.ConvertToInternalUnits(value, unitTypeId);
            }
        }
        catch
        {
            // Fall back to the provided value if conversion fails.
        }

        return value;
    }
}

public sealed class LowVelocityDuctElementResult
{
    public long ElementId { get; set; }
    public string? Category { get; set; }
    public string? Name { get; set; }
    public string? Size { get; set; }
    public double VelocityMetersPerSecond { get; set; }
    public string? VelocityDisplay { get; set; }
}

public sealed class LowVelocityDuctElementsQueryResult
{
    public double MaxVelocityMetersPerSecond { get; set; }
    public int ScannedElements { get; set; }
    public int ElementsWithVelocity { get; set; }
    public int MatchingElements { get; set; }
    public List<long> ElementIds { get; set; } = new();
    public List<LowVelocityDuctElementResult> Items { get; set; } = new();
}

public sealed class DuctDimensionsUpdateResult
{
    public double? WidthMillimeters { get; set; }
    public double? HeightMillimeters { get; set; }
    public List<long> RequestedElementIds { get; set; } = new();
    public List<long> UpdatedElementIds { get; set; } = new();
    public List<long> MissingElementIds { get; set; } = new();
    public List<long> FailedElementIds { get; set; } = new();
    public string Message { get; set; } = "";
}

public sealed class FlexDuctDiameterUpdateResult
{
    public double DiameterMillimeters { get; set; }
    public List<long> RequestedElementIds { get; set; } = new();
    public List<long> UpdatedElementIds { get; set; } = new();
    public List<long> MissingElementIds { get; set; } = new();
    public List<long> FailedElementIds { get; set; } = new();
    public string Message { get; set; } = "";
}

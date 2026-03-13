using Autodesk.Revit.DB;

namespace RevitMcp.Core.Services;

public sealed class SystemAirElementsExtractionService
{
    private static readonly Lazy<HashSet<string>> SystemNameParameterNames = new(CreateSystemNameParameterNames);
    private static readonly Color Blue = new(0, 0, 255);

    public SystemAirElementsExtractionResult ExtractAndColor(Document doc, string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            throw new ArgumentException("systemName is required.", nameof(systemName));
        }

        var view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");
        var normalizedSystemName = systemName.Trim();

        var targetCategoryIds = new List<ElementId>
        {
            new(BuiltInCategory.OST_DuctCurves),
            new(BuiltInCategory.OST_FlexDuctCurves),
            new(BuiltInCategory.OST_DuctTerminal),
            new(BuiltInCategory.OST_DuctFitting)
        };

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(targetCategoryIds))
            .ToList();

        var matchingElements = elements
            .Where(e => e is not null &&
                        string.Equals(GetSystemName(e), normalizedSystemName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var coloredCount = 0;
        var colorMessage = "No matching elements found to color.";
        if (matchingElements.Count > 0)
        {
            if (view.AreGraphicsOverridesAllowed())
            {
                coloredCount = ApplyBlueOverrides(doc, view, matchingElements);
                colorMessage = $"Applied blue overrides in view '{view.Name}' for {coloredCount} elements.";
            }
            else
            {
                colorMessage = $"Active view '{view.Name}' does not allow Visibility/Graphics overrides.";
            }
        }

        var items = matchingElements
            .Select(e => BuildRuleItem(doc, e))
            .ToList();

        return new SystemAirElementsExtractionResult
        {
            RequestedSystemName = normalizedSystemName,
            Summary = new SystemAirElementsExtractionSummary
            {
                ScannedElements = elements.Count,
                MatchedElements = matchingElements.Count,
                ColoredElements = coloredCount,
                ViewId = view.Id.Value,
                ViewName = view.Name,
                ColorMessage = colorMessage
            },
            Items = items
        };
    }

    private static int ApplyBlueOverrides(Document doc, View view, IReadOnlyCollection<Element> elements)
    {
        var applied = 0;
        using var tx = new Transaction(doc, "Color System Elements Blue");
        tx.Start();

        foreach (var element in elements)
        {
            try
            {
                var settings = new OverrideGraphicSettings()
                    .SetProjectionLineColor(Blue)
                    .SetCutLineColor(Blue);

                view.SetElementOverrides(element.Id, settings);
                applied++;
            }
            catch
            {
                // Some elements may not be overridable in this view. Skip and continue.
            }
        }

        tx.Commit();
        return applied;
    }

    private static SystemAirRuleElement BuildRuleItem(Document doc, Element element)
    {
        var elementType = GetElementType(doc, element);

        return new SystemAirRuleElement
        {
            ElementId = element.Id.Value,
            UniqueId = element.UniqueId,
            Category = element.Category?.Name,
            Family = GetFamilyName(element, elementType),
            Type = elementType?.Name ?? element.Name,
            TypeId = elementType?.Id.Value ?? element.GetTypeId().Value,
            SystemName = GetSystemName(element),
            SystemType = GetStringValue(element, elementType, names: new[] { "System Type" }),
            SystemClassification = GetStringValue(element, elementType, names: new[] { "System Classification" }),
            Level = GetLevelName(doc, element, elementType),
            Section = GetIntegerValue(element, elementType, names: new[] { "Section" }),
            Mark = GetStringValue(element, elementType, builtInParameter: BuiltInParameter.ALL_MODEL_MARK, names: new[] { "Mark" }),
            Size = GetStringValue(element, elementType, names: new[] { "Size" }),
            WidthMm = GetLengthInMillimeters(element, elementType, "Width"),
            HeightMm = GetLengthInMillimeters(element, elementType, "Height"),
            DiameterMm = GetLengthInMillimeters(element, elementType, "Diameter"),
            LengthM = GetLengthInMeters(element, elementType, "Length"),
            FlowLs = GetFlowInLitersPerSecond(element, elementType, "Flow"),
            VelocityMs = GetVelocityInMetersPerSecond(element, elementType, "Velocity"),
            FrictionPaPerM = GetFrictionInPascalsPerMeter(element, elementType, "Friction", "Friction Loss"),
            PressureDropPa = GetPressureInPascals(element, elementType, "Pressure Drop"),
            LossCoefficient = GetDoubleValue(element, elementType, names: new[] { "Loss Coefficient" }),
            HydraulicDiameterMm = GetLengthInMillimeters(element, elementType, "Hydraulic Diameter"),
            ReynoldsNumber = GetDoubleValue(element, elementType, names: new[] { "Reynolds Number" }),
            LossMethod = GetStringValue(element, elementType, names: new[] { "Loss Method" }),
            LossMethodSettings = GetStringValue(element, elementType, names: new[] { "Loss Method Settings" }),
            HostId = GetHostId(element),
            AngleDeg = GetAngleInDegrees(element, elementType, "Angle")
        };
    }

    private static string? GetFamilyName(Element element, ElementType? elementType)
    {
        if (elementType?.FamilyName is not null)
        {
            return elementType.FamilyName;
        }

        if (element is FamilyInstance familyInstance)
        {
            return familyInstance.Symbol?.FamilyName;
        }

        return null;
    }

    private static string? GetSystemName(Element element)
    {
        var byBuiltIn = element.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
        var value = ReadParameterText(byBuiltIn);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        foreach (var parameter in element.Parameters.Cast<Parameter>())
        {
            if (!IsSystemNameParameter(parameter.Definition?.Name))
            {
                continue;
            }

            value = ReadParameterText(parameter);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? GetLevelName(Document doc, Element element, ElementType? elementType)
    {
        if (element.LevelId != ElementId.InvalidElementId &&
            doc.GetElement(element.LevelId) is Level level)
        {
            return level.Name;
        }

        return GetStringValue(element, elementType, names: new[] { "Reference Level", "Level" });
    }

    private static long? GetHostId(Element element)
    {
        if (element is FamilyInstance fi && fi.Host is not null)
        {
            return fi.Host.Id.Value;
        }

        return null;
    }

    private static string? GetStringValue(
        Element element,
        ElementType? elementType,
        BuiltInParameter? builtInParameter = null,
        string[]? names = null)
    {
        var parameter = FindParameter(element, elementType, builtInParameter, names);
        return ReadParameterText(parameter);
    }

    private static int? GetIntegerValue(
        Element element,
        ElementType? elementType,
        BuiltInParameter? builtInParameter = null,
        params string[] names)
    {
        var parameter = FindParameter(element, elementType, builtInParameter, names);
        if (parameter is null)
        {
            return null;
        }

        if (parameter.StorageType == StorageType.Integer)
        {
            return parameter.AsInteger();
        }

        if (parameter.StorageType == StorageType.Double)
        {
            return (int)Math.Round(parameter.AsDouble());
        }

        return null;
    }

    private static double? GetDoubleValue(
        Element element,
        ElementType? elementType,
        BuiltInParameter? builtInParameter = null,
        string[]? names = null)
    {
        var parameter = FindParameter(element, elementType, builtInParameter, names);
        if (parameter is null)
        {
            return null;
        }

        return ReadParameterDouble(parameter);
    }

    private static double? GetLengthInMillimeters(Element element, ElementType? elementType, params string[] names)
    {
        return ConvertFromInternalUnits(GetDoubleValue(element, elementType, names: names), "Millimeters");
    }

    private static double? GetLengthInMeters(Element element, ElementType? elementType, params string[] names)
    {
        return ConvertFromInternalUnits(GetDoubleValue(element, elementType, names: names), "Meters");
    }

    private static double? GetFlowInLitersPerSecond(Element element, ElementType? elementType, params string[] names)
    {
        return ConvertFromInternalUnits(GetDoubleValue(element, elementType, names: names), "LitersPerSecond");
    }

    private static double? GetVelocityInMetersPerSecond(Element element, ElementType? elementType, params string[] names)
    {
        return ConvertFromInternalUnits(GetDoubleValue(element, elementType, names: names), "MetersPerSecond");
    }

    private static double? GetFrictionInPascalsPerMeter(Element element, ElementType? elementType, params string[] names)
    {
        return ConvertFromInternalUnits(GetDoubleValue(element, elementType, names: names), "PascalsPerMeter");
    }

    private static double? GetPressureInPascals(Element element, ElementType? elementType, params string[] names)
    {
        return ConvertFromInternalUnits(GetDoubleValue(element, elementType, names: names), "Pascals");
    }

    private static double? GetAngleInDegrees(Element element, ElementType? elementType, params string[] names)
    {
        return ConvertFromInternalUnits(GetDoubleValue(element, elementType, names: names), "Degrees");
    }

    private static double? ConvertFromInternalUnits(double? value, string unitTypeIdName)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var unitProperty = typeof(UnitTypeId).GetProperty(unitTypeIdName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (unitProperty?.GetValue(null) is ForgeTypeId unitTypeId)
            {
                return UnitUtils.ConvertFromInternalUnits(value.Value, unitTypeId);
            }
        }
        catch
        {
            // If conversion fails, return the internal value as fallback.
        }

        return value.Value;
    }

    private static Parameter? FindParameter(
        Element element,
        ElementType? elementType,
        BuiltInParameter? builtInParameter,
        string[]? names)
    {
        if (builtInParameter.HasValue)
        {
            var byBuiltIn = element.get_Parameter(builtInParameter.Value)
                ?? elementType?.get_Parameter(builtInParameter.Value);
            if (byBuiltIn is not null)
            {
                return byBuiltIn;
            }
        }

        if (names is null || names.Length == 0)
        {
            return null;
        }

        foreach (var name in names)
        {
            var byName = FindByName(element, name) ?? FindByName(elementType, name);
            if (byName is not null)
            {
                return byName;
            }
        }

        return null;
    }

    private static Parameter? FindByName(Element? element, string name)
    {
        if (element is null)
        {
            return null;
        }

        foreach (var parameter in element.Parameters.Cast<Parameter>())
        {
            if (string.Equals(parameter.Definition?.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
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

        return SafeAsValueString(parameter);
    }

    private static double? ReadParameterDouble(Parameter parameter)
    {
        switch (parameter.StorageType)
        {
            case StorageType.Double:
                return parameter.AsDouble();
            case StorageType.Integer:
                return parameter.AsInteger();
            default:
                return null;
        }
    }

    private static bool IsSystemNameParameter(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && SystemNameParameterNames.Value.Contains(name);
    }

    private static HashSet<string> CreateSystemNameParameterNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System Name"
        };

        try
        {
            names.Add(LabelUtils.GetLabelFor(BuiltInParameter.RBS_SYSTEM_NAME_PARAM));
        }
        catch
        {
            // Keep defaults if localized labels are unavailable.
        }

        return names;
    }

    private static string? SafeAsValueString(Parameter parameter)
    {
        try
        {
            return parameter.AsValueString();
        }
        catch
        {
            return null;
        }
    }

    private static ElementType? GetElementType(Document doc, Element element)
    {
        var typeId = element.GetTypeId();
        if (typeId == ElementId.InvalidElementId)
        {
            return null;
        }

        return doc.GetElement(typeId) as ElementType;
    }
}

public sealed class SystemAirElementsExtractionResult
{
    public string RequestedSystemName { get; set; } = "";
    public SystemAirElementsExtractionSummary Summary { get; set; } = new();
    public List<SystemAirRuleElement> Items { get; set; } = new();
}

public sealed class SystemAirElementsExtractionSummary
{
    public int ScannedElements { get; set; }
    public int MatchedElements { get; set; }
    public int ColoredElements { get; set; }
    public long ViewId { get; set; }
    public string ViewName { get; set; } = "";
    public string ColorMessage { get; set; } = "";
}

public sealed class SystemAirRuleElement
{
    // Identity + grouping + rule context
    public long ElementId { get; set; }
    public string UniqueId { get; set; } = "";
    public string? Category { get; set; }
    public string? Family { get; set; }
    public string? Type { get; set; }
    public long TypeId { get; set; }
    public string? SystemName { get; set; }
    public string? SystemType { get; set; }
    public string? SystemClassification { get; set; }
    public string? Level { get; set; }
    public int? Section { get; set; }
    public string? Mark { get; set; }

    // Geometry + flow + pressure
    public string? Size { get; set; }
    public double? WidthMm { get; set; }
    public double? HeightMm { get; set; }
    public double? DiameterMm { get; set; }
    public double? LengthM { get; set; }
    public double? FlowLs { get; set; }
    public double? VelocityMs { get; set; }
    public double? FrictionPaPerM { get; set; }
    public double? PressureDropPa { get; set; }
    public double? LossCoefficient { get; set; }
    public double? HydraulicDiameterMm { get; set; }
    public double? ReynoldsNumber { get; set; }

    // Fitting context
    public string? LossMethod { get; set; }
    public string? LossMethodSettings { get; set; }
    public long? HostId { get; set; }
    public double? AngleDeg { get; set; }
}

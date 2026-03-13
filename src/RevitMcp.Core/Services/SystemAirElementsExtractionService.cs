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
            .Where(e => e is not null)
            .ToList();

        var matchingElements = elements
            .Where(e => string.Equals(GetSystemName(e), normalizedSystemName, StringComparison.OrdinalIgnoreCase))
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
            .Select(e => BuildItem(doc, e))
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

    private static SystemAirElementItem BuildItem(Document doc, Element element)
    {
        var elementType = GetElementType(doc, element);
        return new SystemAirElementItem
        {
            Element = new SystemAirElementIdentity
            {
                Id = element.Id.Value,
                UniqueId = element.UniqueId,
                Name = element.Name,
                Class = element.GetType().FullName,
                Category = element.Category?.Name,
                CategoryId = element.Category?.Id.Value,
                TypeId = element.GetTypeId().Value,
                TypeName = elementType?.Name,
                SystemName = GetSystemName(element)
            },
            Properties = new SystemAirElementProperties
            {
                Instance = SerializeParameters(doc, element.Parameters).ToList(),
                Type = elementType is null
                    ? new List<SystemAirElementParameter>()
                    : SerializeParameters(doc, elementType.Parameters).ToList()
            }
        };
    }

    private static IEnumerable<SystemAirElementParameter> SerializeParameters(Document doc, ParameterSet parameterSet)
    {
        return parameterSet
            .Cast<Parameter>()
            .Where(p => p.Definition is not null)
            .OrderBy(p => p.Definition!.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id.Value)
            .Select(p => SerializeParameter(doc, p));
    }

    private static SystemAirElementParameter SerializeParameter(Document doc, Parameter parameter)
    {
        object? rawValue = null;
        string? displayValue = SafeAsValueString(parameter);
        string? referencedElementName = null;

        switch (parameter.StorageType)
        {
            case StorageType.Double:
                rawValue = parameter.AsDouble();
                break;
            case StorageType.Integer:
                rawValue = parameter.AsInteger();
                break;
            case StorageType.String:
                rawValue = parameter.AsString();
                displayValue ??= parameter.AsString();
                break;
            case StorageType.ElementId:
                var elementId = parameter.AsElementId();
                rawValue = elementId.Value;
                if (elementId != ElementId.InvalidElementId)
                {
                    referencedElementName = doc.GetElement(elementId)?.Name;
                }
                break;
            case StorageType.None:
            default:
                break;
        }

        return new SystemAirElementParameter
        {
            Id = parameter.Id.Value,
            Name = parameter.Definition?.Name,
            StorageType = parameter.StorageType.ToString(),
            IsReadOnly = parameter.IsReadOnly,
            IsShared = parameter.IsShared,
            SharedGuid = SafeGetSharedGuid(parameter),
            RawValue = rawValue,
            DisplayValue = displayValue,
            ReferencedElementName = referencedElementName
        };
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

    private static string? SafeGetSharedGuid(Parameter parameter)
    {
        if (!parameter.IsShared)
        {
            return null;
        }

        try
        {
            return parameter.GUID.ToString();
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
    public List<SystemAirElementItem> Items { get; set; } = new();
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

public sealed class SystemAirElementItem
{
    public SystemAirElementIdentity Element { get; set; } = new();
    public SystemAirElementProperties Properties { get; set; } = new();
}

public sealed class SystemAirElementIdentity
{
    public long Id { get; set; }
    public string UniqueId { get; set; } = "";
    public string? Name { get; set; }
    public string? Class { get; set; }
    public string? Category { get; set; }
    public long? CategoryId { get; set; }
    public long TypeId { get; set; }
    public string? TypeName { get; set; }
    public string? SystemName { get; set; }
}

public sealed class SystemAirElementProperties
{
    public List<SystemAirElementParameter> Instance { get; set; } = new();
    public List<SystemAirElementParameter> Type { get; set; } = new();
}

public sealed class SystemAirElementParameter
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public string StorageType { get; set; } = "";
    public bool IsReadOnly { get; set; }
    public bool IsShared { get; set; }
    public string? SharedGuid { get; set; }
    public object? RawValue { get; set; }
    public string? DisplayValue { get; set; }
    public string? ReferencedElementName { get; set; }
}

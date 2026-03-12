using Autodesk.Revit.DB;

namespace RevitMcp.Core.Services;

public sealed class DuctPressureDropService
{
    private static readonly Lazy<HashSet<string>> PressureDropParameterNames = new(CreatePressureDropParameterNames);

    private static readonly BuiltInParameter[] PressureDropParameterPriority =
    {
        BuiltInParameter.RBS_DUCT_PRESSURE_DROP
    };

    private static readonly GradientStop[] GradientStops =
    {
        new(0.00, new Color(0, 76, 255)),
        new(0.25, new Color(0, 170, 255)),
        new(0.50, new Color(0, 214, 154)),
        new(0.75, new Color(255, 201, 32)),
        new(1.00, new Color(225, 59, 40))
    };

    public object ColorizeAndGetTopPressureDrop(Document doc, int topCount = 10)
    {
        var view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");
        topCount = Math.Max(1, topCount);

        var targetCategoryIds = new List<ElementId>
        {
            new(BuiltInCategory.OST_DuctCurves),
            new(BuiltInCategory.OST_FlexDuctCurves),
            new(BuiltInCategory.OST_DuctFitting)
        };

        var collector = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(targetCategoryIds));

        var candidates = new List<ElementPressureCandidate>();
        var scannedElements = 0;

        foreach (var element in collector)
        {
            scannedElements++;

            if (element is null)
            {
                continue;
            }

            if (!TryGetPressureDrop(element, out var pressureDrop, out var pressureParameter))
            {
                continue;
            }

            candidates.Add(new ElementPressureCandidate(
                element,
                pressureDrop,
                pressureParameter.Definition?.Name ?? "Pressure Drop",
                SafeAsValueString(pressureParameter)));
        }

        var top = candidates
            .OrderByDescending(x => x.PressureDrop)
            .Take(topCount)
            .ToList();

        var coloredCount = 0;
        var colorApplyMessage = "No pressure drop values were found.";
        var colorBar = Array.Empty<object>();

        if (candidates.Count > 0)
        {
            var min = candidates.Min(x => x.PressureDrop);
            var max = candidates.Max(x => x.PressureDrop);
            colorBar = BuildColorBar(min, max).ToArray();

            if (view.AreGraphicsOverridesAllowed())
            {
                coloredCount = ApplyPressureDropColors(doc, view, candidates, min, max);
                colorApplyMessage = $"Applied gradient overrides in view '{view.Name}' for {coloredCount} elements.";
            }
            else
            {
                colorApplyMessage = $"Active view '{view.Name}' does not allow Visibility/Graphics overrides.";
            }
        }

        var topItems = top
            .Select((x, index) => BuildTopItem(doc, x, index + 1))
            .ToList();

        return new
        {
            Summary = new
            {
                ScannedElements = scannedElements,
                PressureDropElements = candidates.Count,
                MissingPressureDrop = scannedElements - candidates.Count,
                TopCountRequested = topCount,
                TopCountReturned = topItems.Count
            },
            Coloring = new
            {
                Applied = coloredCount > 0,
                ColoredElements = coloredCount,
                ViewId = view.Id.Value,
                ViewName = view.Name,
                Message = colorApplyMessage
            },
            ColorBar = colorBar,
            Top = topItems
        };
    }

    private static IEnumerable<object> BuildColorBar(double min, double max)
    {
        if (max < min)
        {
            yield break;
        }

        const int steps = 6;
        for (var i = 0; i <= steps; i++)
        {
            var ratio = i / (double)steps;
            var sample = min + ((max - min) * ratio);
            var color = GetColdToWarmColor(sample, min, max);
            yield return new
            {
                Stop = ratio,
                PressureDropInternal = sample,
                ColorHex = ToHex(color),
                ColorRgb = new { color.Red, color.Green, color.Blue }
            };
        }
    }

    private static object BuildTopItem(Document doc, ElementPressureCandidate candidate, int rank)
    {
        var element = candidate.Element;
        var type = GetElementType(doc, element);

        return new
        {
            Rank = rank,
            PressureDrop = new
            {
                Internal = candidate.PressureDrop,
                Display = candidate.PressureDropDisplay,
                SourceParameter = candidate.SourceParameterName
            },
            Element = new
            {
                Id = element.Id.Value,
                UniqueId = element.UniqueId,
                Name = element.Name,
                Class = element.GetType().FullName,
                Category = element.Category?.Name,
                CategoryId = element.Category?.Id.Value,
                TypeId = element.GetTypeId().Value,
                TypeName = type?.Name
            },
            Properties = new
            {
                Instance = SerializeParameters(doc, element.Parameters),
                Type = type is null ? Array.Empty<object>() : SerializeParameters(doc, type.Parameters)
            }
        };
    }

    private static IEnumerable<object> SerializeParameters(Document doc, ParameterSet parameterSet)
    {
        return parameterSet
            .Cast<Parameter>()
            .Where(p => p.Definition is not null)
            .OrderBy(p => p.Definition!.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id.Value)
            .Select(p => SerializeParameter(doc, p))
            .ToList();
    }

    private static object SerializeParameter(Document doc, Parameter parameter)
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
                if (displayValue is null)
                {
                    displayValue = parameter.AsString();
                }
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

        return new
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

    private static int ApplyPressureDropColors(
        Document doc,
        View view,
        IReadOnlyCollection<ElementPressureCandidate> elements,
        double min,
        double max)
    {
        var applied = 0;

        using var tx = new Transaction(doc, "Colorize Duct Pressure Drop");
        tx.Start();

        foreach (var candidate in elements)
        {
            var color = GetColdToWarmColor(candidate.PressureDrop, min, max);
            var settings = new OverrideGraphicSettings()
                .SetProjectionLineColor(color)
                .SetCutLineColor(color);

            try
            {
                view.SetElementOverrides(candidate.Element.Id, settings);
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

    private static Color GetColdToWarmColor(double value, double min, double max)
    {
        if (max <= min)
        {
            return GradientStops[GradientStops.Length - 1].Color;
        }

        var normalized = Clamp((value - min) / (max - min), 0d, 1d);
        var lower = GradientStops[0];
        var upper = GradientStops[GradientStops.Length - 1];

        for (var i = 0; i < GradientStops.Length - 1; i++)
        {
            var current = GradientStops[i];
            var next = GradientStops[i + 1];

            if (normalized >= current.Stop && normalized <= next.Stop)
            {
                lower = current;
                upper = next;
                break;
            }
        }

        var interval = upper.Stop - lower.Stop;
        var blend = interval <= 0 ? 0 : (normalized - lower.Stop) / interval;

        static byte Lerp(byte a, byte b, double t) => (byte)Math.Round(a + ((b - a) * t));

        return new Color(
            Lerp(lower.Color.Red, upper.Color.Red, blend),
            Lerp(lower.Color.Green, upper.Color.Green, blend),
            Lerp(lower.Color.Blue, upper.Color.Blue, blend));
    }

    private static bool TryGetPressureDrop(Element element, out double pressureDrop, out Parameter pressureParameter)
    {
        foreach (var builtInParameter in PressureDropParameterPriority)
        {
            var parameter = element.get_Parameter(builtInParameter);
            if (TryGetNumericValue(parameter, out pressureDrop))
            {
                pressureParameter = parameter!;
                return true;
            }
        }

        foreach (var parameter in element.Parameters.Cast<Parameter>())
        {
            if (!IsPressureDropParameterName(parameter.Definition?.Name))
            {
                continue;
            }

            if (TryGetNumericValue(parameter, out pressureDrop))
            {
                pressureParameter = parameter;
                return true;
            }
        }

        pressureDrop = 0;
        pressureParameter = null!;
        return false;
    }

    private static bool TryGetNumericValue(Parameter? parameter, out double value)
    {
        value = 0;
        if (parameter is null)
        {
            return false;
        }

        switch (parameter.StorageType)
        {
            case StorageType.Double:
                value = parameter.AsDouble();
                break;
            case StorageType.Integer:
                value = parameter.AsInteger();
                break;
            default:
                return false;
        }

        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string ToHex(Color color)
    {
        return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
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

    private sealed class ElementPressureCandidate
    {
        public ElementPressureCandidate(Element element, double pressureDrop, string sourceParameterName, string? pressureDropDisplay)
        {
            Element = element;
            PressureDrop = pressureDrop;
            SourceParameterName = sourceParameterName;
            PressureDropDisplay = pressureDropDisplay;
        }

        public Element Element { get; }
        public double PressureDrop { get; }
        public string SourceParameterName { get; }
        public string? PressureDropDisplay { get; }
    }

    private sealed class GradientStop
    {
        public GradientStop(double stop, Color color)
        {
            Stop = stop;
            Color = color;
        }

        public double Stop { get; }
        public Color Color { get; }
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static bool IsPressureDropParameterName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && PressureDropParameterNames.Value.Contains(name);
    }

    private static HashSet<string> CreatePressureDropParameterNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pressure Drop"
        };

        try
        {
            names.Add(LabelUtils.GetLabelFor(BuiltInParameter.RBS_DUCT_PRESSURE_DROP));
        }
        catch
        {
            // Keep defaults if localized labels are unavailable.
        }

        return names;
    }
}

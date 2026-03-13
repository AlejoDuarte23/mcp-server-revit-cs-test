using Autodesk.Revit.DB;
using System.Globalization;

namespace RevitMcp.Core.Services;

public sealed class DuctFittingLossService
{
    private const int SpecificCoefficientLossMethodValue = 6; // DuctLossMethodType.Coefficient
    private static readonly Lazy<HashSet<string>> SystemNameParameterNames = new(CreateSystemNameParameterNames);
    private static readonly Color ZeroPressureColor = new(255, 0, 0);

    private static readonly BuiltInParameter[] PressureDropParameterPriority =
    {
        BuiltInParameter.RBS_DUCT_PRESSURE_DROP,
        BuiltInParameter.RBS_PRESSURE_DROP
    };

    public ZeroPressureFittingCheckResult ColorZeroPressureDropFittings(
        Document doc,
        string systemName,
        double zeroTolerance = 1e-9)
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            throw new ArgumentException("systemName is required.", nameof(systemName));
        }

        var view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");
        var normalizedSystemName = systemName.Trim();

        var allFittings = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfCategory(BuiltInCategory.OST_DuctFitting)
            .ToElements()
            .ToList();

        var systemFittings = allFittings
            .Where(x => string.Equals(GetSystemName(x), normalizedSystemName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var zeroPressureFittings = systemFittings
            .Where(x => TryGetPressureDrop(x, out var pressureDrop) && IsZero(pressureDrop, zeroTolerance))
            .ToList();

        var coloredCount = 0;
        var colorMessage = "No zero-pressure fittings found to color.";

        if (zeroPressureFittings.Count > 0)
        {
            if (view.AreGraphicsOverridesAllowed())
            {
                coloredCount = ApplyColorOverrides(doc, view, zeroPressureFittings, ZeroPressureColor, "Color Zero Pressure Drop Fittings");
                colorMessage = $"Applied color overrides in view '{view.Name}' for {coloredCount} fittings.";
            }
            else
            {
                colorMessage = $"Active view '{view.Name}' does not allow Visibility/Graphics overrides.";
            }
        }

        return new ZeroPressureFittingCheckResult
        {
            SystemName = normalizedSystemName,
            Summary = new ZeroPressureFittingCheckSummary
            {
                TotalFittings = allFittings.Count,
                SystemFittings = systemFittings.Count,
                ZeroPressureFittings = zeroPressureFittings.Count,
                ColoredFittings = coloredCount,
                ViewId = view.Id.Value,
                ViewName = view.Name,
                Message = colorMessage
            },
            ElementIds = zeroPressureFittings.Select(x => x.Id.Value).ToList()
        };
    }

    public SetFittingSpecificCoefficientResult SetSpecificCoefficient(
        Document doc,
        string systemName,
        double coefficient = 0.2,
        bool onlyZeroPressure = true,
        double zeroTolerance = 1e-9)
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            throw new ArgumentException("systemName is required.", nameof(systemName));
        }

        var normalizedSystemName = systemName.Trim();

        var allFittings = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfCategory(BuiltInCategory.OST_DuctFitting)
            .ToElements()
            .ToList();

        var systemFittings = allFittings
            .Where(x => string.Equals(GetSystemName(x), normalizedSystemName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var targetFittings = onlyZeroPressure
            ? systemFittings.Where(x => TryGetPressureDrop(x, out var pressureDrop) && IsZero(pressureDrop, zeroTolerance)).ToList()
            : systemFittings;

        var updatedElementIds = new List<long>();
        var failedElementIds = new List<long>();

        using var tx = new Transaction(doc, "Set Duct Fitting Specific Coefficient");
        tx.Start();

        foreach (var fitting in targetFittings)
        {
            try
            {
                var updated = false;
                updated |= TrySetLossMethodToSpecificCoefficient(fitting);
                updated |= TrySetLossMethodSettings(fitting, coefficient);
                updated |= TrySetLossCoefficient(fitting, coefficient);

                if (updated)
                {
                    updatedElementIds.Add(fitting.Id.Value);
                }
                else
                {
                    failedElementIds.Add(fitting.Id.Value);
                }
            }
            catch
            {
                failedElementIds.Add(fitting.Id.Value);
            }
        }

        tx.Commit();

        return new SetFittingSpecificCoefficientResult
        {
            SystemName = normalizedSystemName,
            Coefficient = coefficient,
            OnlyZeroPressure = onlyZeroPressure,
            UpdatedElementIds = updatedElementIds,
            FailedElementIds = failedElementIds
        };
    }

    private static bool TrySetLossMethodToSpecificCoefficient(Element fitting)
    {
        var methodParam =
            fitting.get_Parameter(BuiltInParameter.RBS_DUCT_FITTING_LOSS_METHOD_PARAM) ??
            fitting.get_Parameter(BuiltInParameter.RBS_DUCT_FITTING_LOSS_METHOD_SERVER_PARAM);

        if (methodParam is null || methodParam.IsReadOnly)
        {
            return false;
        }

        if (methodParam.StorageType == StorageType.Integer)
        {
            return methodParam.Set(SpecificCoefficientLossMethodValue);
        }

        if (methodParam.StorageType == StorageType.String)
        {
            return methodParam.Set("Specific Coefficient");
        }

        return methodParam.SetValueString("Specific Coefficient");
    }

    private static bool TrySetLossMethodSettings(Element fitting, double coefficient)
    {
        var settingsParam = fitting.get_Parameter(BuiltInParameter.RBS_DUCT_FITTING_LOSS_METHOD_SETTINGS);
        if (settingsParam is null || settingsParam.IsReadOnly)
        {
            return false;
        }

        return SetNumericParameter(settingsParam, coefficient);
    }

    private static bool TrySetLossCoefficient(Element fitting, double coefficient)
    {
        var coefficientParam = fitting.get_Parameter(BuiltInParameter.RBS_LOSS_COEFFICIENT);
        if (coefficientParam is null || coefficientParam.IsReadOnly)
        {
            return false;
        }

        return SetNumericParameter(coefficientParam, coefficient);
    }

    private static bool SetNumericParameter(Parameter parameter, double value)
    {
        switch (parameter.StorageType)
        {
            case StorageType.Double:
                return parameter.Set(value);
            case StorageType.Integer:
                return parameter.Set((int)Math.Round(value));
            case StorageType.String:
                return parameter.Set(value.ToString(CultureInfo.InvariantCulture));
            default:
                return parameter.SetValueString(value.ToString("G", CultureInfo.InvariantCulture));
        }
    }

    private static int ApplyColorOverrides(
        Document doc,
        View view,
        IReadOnlyCollection<Element> elements,
        Color color,
        string transactionName)
    {
        var applied = 0;

        using var tx = new Transaction(doc, transactionName);
        tx.Start();

        foreach (var element in elements)
        {
            try
            {
                var settings = new OverrideGraphicSettings()
                    .SetProjectionLineColor(color)
                    .SetCutLineColor(color);

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

    private static bool TryGetPressureDrop(Element element, out double pressureDrop)
    {
        foreach (var builtInParameter in PressureDropParameterPriority)
        {
            var parameter = element.get_Parameter(builtInParameter);
            if (parameter is null)
            {
                continue;
            }

            switch (parameter.StorageType)
            {
                case StorageType.Double:
                    pressureDrop = parameter.AsDouble();
                    return !double.IsNaN(pressureDrop) && !double.IsInfinity(pressureDrop);
                case StorageType.Integer:
                    pressureDrop = parameter.AsInteger();
                    return true;
            }
        }

        pressureDrop = 0;
        return false;
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

    private static bool IsZero(double value, double tolerance)
    {
        return Math.Abs(value) <= tolerance;
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
}

public sealed class ZeroPressureFittingCheckResult
{
    public string SystemName { get; set; } = "";
    public ZeroPressureFittingCheckSummary Summary { get; set; } = new();
    public List<long> ElementIds { get; set; } = new();
}

public sealed class ZeroPressureFittingCheckSummary
{
    public int TotalFittings { get; set; }
    public int SystemFittings { get; set; }
    public int ZeroPressureFittings { get; set; }
    public int ColoredFittings { get; set; }
    public long ViewId { get; set; }
    public string ViewName { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class SetFittingSpecificCoefficientResult
{
    public string SystemName { get; set; } = "";
    public double Coefficient { get; set; }
    public bool OnlyZeroPressure { get; set; }
    public List<long> UpdatedElementIds { get; set; } = new();
    public List<long> FailedElementIds { get; set; } = new();
}

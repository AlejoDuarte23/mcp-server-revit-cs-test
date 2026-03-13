using Autodesk.Revit.DB;

namespace RevitMcp.Core.Services;

public sealed class ElementColorOverrideService
{
    public ElementColorOverrideResult ApplyColor(
        Document doc,
        IEnumerable<long> elementIds,
        byte red,
        byte green,
        byte blue)
    {
        if (elementIds is null)
        {
            throw new ArgumentNullException(nameof(elementIds));
        }

        var requestedElementIds = elementIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (requestedElementIds.Count == 0)
        {
            throw new ArgumentException("At least one positive element ID is required.", nameof(elementIds));
        }

        var view = doc.ActiveView ?? throw new InvalidOperationException("No active view.");
        var appliedElementIds = new List<long>();
        var missingElementIds = new List<long>();
        var failedElementIds = new List<long>();
        var color = new Color(red, green, blue);

        var elements = requestedElementIds
            .Select(id => new { Id = id, Element = doc.GetElement(new ElementId(id)) })
            .ToList();

        foreach (var entry in elements.Where(x => x.Element is null))
        {
            missingElementIds.Add(entry.Id);
        }

        var existingElements = elements
            .Where(x => x.Element is not null)
            .Select(x => new ElementIdEntry(x.Id, x.Element!))
            .ToList();

        var message = $"Applied color overrides in view '{view.Name}' for 0 elements.";
        if (!view.AreGraphicsOverridesAllowed())
        {
            message = $"Active view '{view.Name}' does not allow Visibility/Graphics overrides.";
        }
        else if (existingElements.Count > 0)
        {
            var settings = CreateOverrideGraphicSettings(doc, color);

            using var tx = new Transaction(doc, "Color Elements By Id");
            tx.Start();

            foreach (var entry in existingElements)
            {
                try
                {
                    view.SetElementOverrides(entry.Element.Id, settings);
                    appliedElementIds.Add(entry.Id);
                }
                catch
                {
                    failedElementIds.Add(entry.Id);
                }
            }

            tx.Commit();
            message = $"Applied color overrides in view '{view.Name}' for {appliedElementIds.Count} of {requestedElementIds.Count} requested elements.";
        }

        return new ElementColorOverrideResult
        {
            ViewId = view.Id.Value,
            ViewName = view.Name,
            Red = red,
            Green = green,
            Blue = blue,
            RequestedElementIds = requestedElementIds,
            AppliedElementIds = appliedElementIds,
            MissingElementIds = missingElementIds,
            FailedElementIds = failedElementIds,
            Message = message
        };
    }

    private static OverrideGraphicSettings CreateOverrideGraphicSettings(Document doc, Color color)
    {
        var settings = new OverrideGraphicSettings()
            .SetProjectionLineColor(color)
            .SetCutLineColor(color);

        var solidFillPatternId = GetSolidFillPatternId(doc);
        if (solidFillPatternId == ElementId.InvalidElementId)
        {
            return settings;
        }

        return settings
            .SetSurfaceForegroundPatternId(solidFillPatternId)
            .SetSurfaceForegroundPatternColor(color)
            .SetSurfaceForegroundPatternVisible(true)
            .SetCutForegroundPatternId(solidFillPatternId)
            .SetCutForegroundPatternColor(color)
            .SetCutForegroundPatternVisible(true);
    }

    private static ElementId GetSolidFillPatternId(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(x => x.GetFillPattern().IsSolidFill)?.Id ?? ElementId.InvalidElementId;
    }

    private sealed record ElementIdEntry(long Id, Element Element);
}

public sealed class ElementColorOverrideResult
{
    public long ViewId { get; set; }
    public string ViewName { get; set; } = "";
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
    public List<long> RequestedElementIds { get; set; } = new();
    public List<long> AppliedElementIds { get; set; } = new();
    public List<long> MissingElementIds { get; set; } = new();
    public List<long> FailedElementIds { get; set; } = new();
    public string Message { get; set; } = "";
}

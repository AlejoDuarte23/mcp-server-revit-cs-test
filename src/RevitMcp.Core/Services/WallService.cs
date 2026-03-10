using Autodesk.Revit.DB;

namespace RevitMcp.Core.Services;

public sealed class WallService
{
    public object ListWalls(Document doc)
    {
        var walls = new FilteredElementCollector(doc)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .Select(w => new
            {
                // Use .Value for all Revit versions (IntegerValue is deprecated in 2024+)
                Id = w.Id.Value,
                Name = w.Name,
                Category = w.Category?.Name
            })
            .ToList();

        return new
        {
            Count = walls.Count,
            Items = walls
        };
    }
}


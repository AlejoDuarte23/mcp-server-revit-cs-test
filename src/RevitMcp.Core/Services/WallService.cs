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


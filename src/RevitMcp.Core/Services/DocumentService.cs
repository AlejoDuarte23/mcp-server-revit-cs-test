using Autodesk.Revit.DB;

namespace RevitMcp.Core.Services;

public sealed class DocumentService
{
    public object GetActiveDocumentInfo(Document doc)
    {
        return new
        {
            Title = doc.Title,
            PathName = doc.PathName,
            IsFamilyDocument = doc.IsFamilyDocument,
            IsWorkshared = doc.IsWorkshared
        };
    }
}


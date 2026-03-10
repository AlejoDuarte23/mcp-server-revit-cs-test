using Autodesk.Revit.UI;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class GetActiveDocumentHandler : IRevitCommandHandler
{
    public string Name => "get_active_document";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var service = new DocumentService();
        return service.GetActiveDocumentInfo(doc);
    }
}


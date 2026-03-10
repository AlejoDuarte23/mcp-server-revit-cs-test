using Autodesk.Revit.UI;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class PingHandler : IRevitCommandHandler
{
    public string Name => "ping";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        return new
        {
            Status = "ok",
            Product = uiApp.Application.VersionName,
            VersionNumber = uiApp.Application.VersionNumber
        };
    }
}


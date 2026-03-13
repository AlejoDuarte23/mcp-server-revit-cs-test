using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using RevitMcp.RevitAddin.Bridge;
using RevitMcp.RevitAddin.Handlers;

namespace RevitMcp.RevitAddin;

public sealed class App : IExternalApplication
{
    private BridgeRequestBroker? _broker;

    internal static LocalHttpBridge? Bridge { get; private set; }

    public Result OnStartup(UIControlledApplication application)
    {
        application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        application.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;

        Bridge?.Dispose();
        Bridge = null;

        _broker?.Dispose();
        _broker = null;

        return Result.Succeeded;
    }

    private void OnApplicationInitialized(object? sender, ApplicationInitializedEventArgs args)
    {
        var dispatcher = new CommandDispatcher(new IRevitCommandHandler[]
        {
            new PingHandler(),
            new GetActiveDocumentHandler(),
            new ListWallsHandler(),
            new ColorizeDuctPressureDropHandler(),
            new ExtractSystemAirElementsHandler(),
            new CheckZeroPressureDropFittingsHandler(),
            new SetFittingSpecificCoefficientHandler()
        });

        _broker = new BridgeRequestBroker(dispatcher);
        Bridge = new LocalHttpBridge(_broker);
        Bridge.Start("http://127.0.0.1:5057/");
    }
}

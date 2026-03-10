using RevitMcp.Server;
using RevitMcp.Server.Tools;

var builder = WebApplication.CreateBuilder(args);
var bridgeUrl =
    builder.Configuration["REVIT_MCP_BRIDGE_URL"] ??
    builder.Configuration["RevitBridge:Url"] ??
    "http://127.0.0.1:5057/";

var serverUrl =
    builder.Configuration["REVIT_MCP_SERVER_URL"] ??
    builder.Configuration["Server:Url"] ??
    "http://127.0.0.1:5099";

builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new BridgeClient(httpClientFactory.CreateClient(), bridgeUrl);
});
builder.Services.AddSingleton<RevitTools>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Name = "Revit MCP demo server",
    Bridge = bridgeUrl,
    Endpoints = new[]
    {
        "/tools/ping",
        "/tools/get_active_document",
        "/tools/list_walls"
    }
}));

app.MapGet("/tools/ping", async (RevitTools tools, CancellationToken ct) => Results.Ok(await tools.Ping(ct)));
app.MapGet("/tools/get_active_document", async (RevitTools tools, CancellationToken ct) => Results.Ok(await tools.GetActiveDocument(ct)));
app.MapGet("/tools/list_walls", async (RevitTools tools, CancellationToken ct) => Results.Ok(await tools.ListWalls(ct)));

app.Run(serverUrl);

using RevitMcp.Contracts;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Bridge;

internal sealed class LocalHttpBridge : IDisposable
{
    private readonly BridgeRequestBroker _broker;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public LocalHttpBridge(BridgeRequestBroker broker)
    {
        _broker = broker;
    }

    public void Start(string prefix)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _cts?.Dispose();
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync();
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context), cancellationToken);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        BridgeResponse response;

        try
        {
            if (context.Request.HttpMethod != "POST")
            {
                context.Response.StatusCode = 405;
                response = new BridgeResponse(false, null, "Only POST is supported.");
            }
            else
            {
                var contentEncoding = context.Request.ContentEncoding ?? Encoding.UTF8;
                using var reader = new StreamReader(context.Request.InputStream, contentEncoding);
                var body = await reader.ReadToEndAsync();

                var request = JsonSerializer.Deserialize<BridgeRequest>(body, _jsonOptions)
                    ?? throw new InvalidOperationException("Invalid request.");

                var result = await _broker.InvokeAsync(request.Tool, request.Arguments);
                response = new BridgeResponse(true, result, null);
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            response = new BridgeResponse(false, null, ex.Message);
        }

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        context.Response.ContentType = "application/json";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        context.Response.Close();
    }
}

using RevitMcp.Contracts;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Bridge;

internal sealed class LocalHttpBridge : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

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
        try
        {
            if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 200;
                await WriteJsonAsync(context.Response, new
                {
                    Name = "Revit MCP bridge",
                    Status = "listening",
                    Method = "POST",
                    RevitExecution = "ExternalEvent"
                });
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 405;
                context.Response.Headers["Allow"] = "GET, POST";
                await WriteJsonAsync(context.Response, new BridgeResponse(false, null, "Only GET and POST are supported."));
                return;
            }

            var contentEncoding = context.Request.ContentEncoding ?? Encoding.UTF8;
            using var reader = new StreamReader(context.Request.InputStream, contentEncoding);
            var body = await reader.ReadToEndAsync();

            var request = JsonSerializer.Deserialize<BridgeRequest>(body, _jsonOptions)
                ?? throw new InvalidOperationException("Invalid request.");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts?.Token ?? CancellationToken.None);
            timeoutCts.CancelAfter(RequestTimeout);

            var result = await _broker.InvokeAsync(request.Tool, request.Arguments, timeoutCts.Token);
            context.Response.StatusCode = 200;
            await WriteJsonAsync(context.Response, new BridgeResponse(true, result, null));
        }
        catch (JsonException ex)
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context.Response, new BridgeResponse(false, null, $"Invalid JSON request: {ex.Message}"));
        }
        catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true)
        {
            context.Response.StatusCode = 503;
            await WriteJsonAsync(context.Response, new BridgeResponse(false, null, "Bridge is shutting down."));
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = 504;
            await WriteJsonAsync(context.Response, new BridgeResponse(false, null, $"Bridge request timed out after {RequestTimeout.TotalSeconds:0} seconds while waiting for Revit."));
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context.Response, new BridgeResponse(false, null, ex.Message));
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch
            {
            }
        }
    }

    private async Task WriteJsonAsync(HttpListenerResponse response, object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = bytes.Length;
            response.Headers["Cache-Control"] = "no-store";
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
        catch (HttpListenerException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}

using RevitMcp.Contracts;
using System.Text;
using System.Text.Json;

namespace RevitMcp.Server;

public sealed class BridgeClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public BridgeClient(HttpClient httpClient, string endpoint)
    {
        _httpClient = httpClient;
        _endpoint = endpoint.TrimEnd('/');
    }

    public async Task<BridgeResponse> InvokeAsync(string tool, object args, CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(tool, JsonSerializer.SerializeToElement(args, _jsonOptions));
        var json = JsonSerializer.Serialize(request, _jsonOptions);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        try
        {
            using var response = await _httpClient.PostAsync(_endpoint, content, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = TryDeserialize(body);
                if (errorResponse is not null)
                {
                    return errorResponse;
                }

                return new BridgeResponse(false, null, $"Revit bridge returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            return TryDeserialize(body) ?? new BridgeResponse(false, null, "Failed to parse bridge response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new BridgeResponse(false, null, $"Revit bridge timed out after {RequestTimeout.TotalSeconds:0} seconds. Verify Revit is idle and the bridge add-in is loaded.");
        }
        catch (HttpRequestException ex)
        {
            return new BridgeResponse(false, null, $"Failed to reach Revit bridge at {_endpoint}: {ex.Message}");
        }
    }

    private BridgeResponse? TryDeserialize(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BridgeResponse>(body, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

using Autodesk.Revit.UI;
using System.Collections.Concurrent;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Bridge;

internal sealed class BridgeRequestBroker : IDisposable
{
    private static readonly TimeSpan RaiseRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly CommandDispatcher _dispatcher;
    private readonly ConcurrentQueue<PendingBridgeRequest> _queue = new();
    private readonly BridgeExternalEventHandler _handler;
    private readonly ExternalEvent _externalEvent;
    private readonly SemaphoreSlim _workSignal = new(0);
    private readonly CancellationTokenSource _schedulerCts = new();
    private readonly Task _schedulerTask;
    private bool _disposed;
    private int _eventInFlight;

    public BridgeRequestBroker(CommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _handler = new BridgeExternalEventHandler(ProcessQueue);
        _externalEvent = ExternalEvent.Create(_handler);
        _schedulerTask = Task.Run(() => SchedulerLoopAsync(_schedulerCts.Token));
    }

    public Task<object?> InvokeAsync(string tool, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var pending = new PendingBridgeRequest(tool, arguments.Clone(), cancellationToken);
        _queue.Enqueue(pending);
        SignalWorkAvailable();

        return pending.Task;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _schedulerCts.Cancel();

        while (_queue.TryDequeue(out var pending))
        {
            pending.TrySetException(new ObjectDisposedException(nameof(BridgeRequestBroker)));
        }

        try
        {
            _schedulerTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _schedulerCts.Dispose();
        _workSignal.Dispose();
        _externalEvent.Dispose();
    }

    private async Task SchedulerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await _workSignal.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (_disposed || _queue.IsEmpty)
                {
                    continue;
                }

                if (Interlocked.CompareExchange(ref _eventInFlight, 1, 0) != 0)
                {
                    continue;
                }

                while (!_disposed && !_queue.IsEmpty)
                {
                    var raiseRequest = _externalEvent.Raise();
                    if (raiseRequest == ExternalEventRequest.Accepted)
                    {
                        break;
                    }

                    if (raiseRequest == ExternalEventRequest.Pending)
                    {
                        await Task.Delay(RaiseRetryDelay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    Interlocked.Exchange(ref _eventInFlight, 0);
                    FailPendingRequests(new InvalidOperationException($"Failed to schedule Revit command. ExternalEvent returned {raiseRequest}."));
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ProcessQueue(UIApplication uiApp)
    {
        try
        {
            while (_queue.TryDequeue(out var pending))
            {
                if (pending.Task.IsCompleted)
                {
                    continue;
                }

                try
                {
                    var result = _dispatcher.Dispatch(uiApp, pending.Tool, pending.Arguments);
                    pending.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    pending.TrySetException(ex);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _eventInFlight, 0);
            if (!_disposed && !_queue.IsEmpty)
            {
                SignalWorkAvailable();
            }
        }
    }

    private void SignalWorkAvailable()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _workSignal.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void FailPendingRequests(Exception ex)
    {
        while (_queue.TryDequeue(out var pending))
        {
            pending.TrySetException(ex);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BridgeRequestBroker));
        }
    }

    private sealed class PendingBridgeRequest
    {
        private readonly TaskCompletionSource<object?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingBridgeRequest(string tool, JsonElement arguments, CancellationToken cancellationToken)
        {
            Tool = tool;
            Arguments = arguments;

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(static state => ((PendingBridgeRequest)state!).TrySetCanceled(), this);
            }
        }

        public string Tool { get; }

        public JsonElement Arguments { get; }

        public Task<object?> Task => _tcs.Task;

        public void TrySetException(Exception ex)
        {
            _tcs.TrySetException(ex);
        }

        public void TrySetResult(object? result)
        {
            _tcs.TrySetResult(result);
        }

        public void TrySetCanceled()
        {
            _tcs.TrySetCanceled();
        }
    }
}

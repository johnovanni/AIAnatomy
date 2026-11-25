using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// What drives a lab's Play button: calls a tick at a regular interval until the returned handle is disposed. The lab
/// takes one in its constructor, so a test can tick by hand and nothing waits on real time.
/// </summary>
public interface IPlayClock
{
    /// <summary>Starts calling <paramref name="tick"/> every <paramref name="interval"/>; disposing the result stops it.</summary>
    IDisposable Start(TimeSpan interval, Action tick);
}

/// <summary>
/// The app's play clock: a <see cref="PeriodicTimer"/> awaited on the caller's SynchronizationContext, so in the app every
/// tick runs on the UI thread (with none, on a thread-pool thread). A tick that throws is logged and ends the clock.
/// </summary>
public sealed class TimerPlayClock : IPlayClock
{
    public IDisposable Start(TimeSpan interval, Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval), "The interval must be positive.");
        var cts = new CancellationTokenSource();
        _ = RunAsync(interval, tick, cts.Token);
        return new Handle(cts);
    }

    private static async Task RunAsync(TimeSpan interval, Action tick, CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(token))
            {
                if (token.IsCancellationRequested) return;
                tick();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            ErrorLog.ReportBackground(e);
        }
    }

    private sealed class Handle(CancellationTokenSource cts) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) cts.Cancel();
        }
    }
}

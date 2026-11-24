using System.Collections;
using System.Globalization;
using System.Text;
using AIAnatomy.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation;

/// <summary>Base class of every lab's view-model.</summary>
public abstract partial class LabViewModel : ObservableObject
{
    public abstract string LabId { get; }

    [ObservableProperty] private int seed = 1;

    /// <summary>Puts the lab back to its defaults.</summary>
    public abstract void Reset();

    /// <summary>Called when the user switches to another model.</summary>
    public virtual void OnActiveModelChanged() { }

    /// <summary>Completes when any background work started on creation has finished (the PNG diagnostic waits for it).</summary>
    public virtual Task WorkCompleted => Task.CompletedTask;

    /// <summary>Called when the user leaves this lab (unless <see cref="KeepsRunningWhenLeft"/>), on Reset where the lab
    /// says so, and when the app closes. Labs cancel their background work here.</summary>
    public virtual void Deactivate() { }

    /// <summary>
    /// True for a lab whose one instance is kept while the user visits other lessons and whose work carries on meanwhile
    /// (the Train lab, ruling R35). Leaving such a lab does not call <see cref="Deactivate"/>.
    /// </summary>
    public virtual bool KeepsRunningWhenLeft => false;

    /// <summary>Called when the seed changes (labs that use randomness redo what depends on it).</summary>
    protected virtual void OnSeedEdited() { }

    partial void OnSeedChanged(int value) => OnSeedEdited();

    private readonly object _screenLock = new();
    private readonly Dictionary<string, object?> _lastScreenValues = new(StringComparer.Ordinal);
    private readonly List<string> _reportedScreens = [];
    private Dictionary<string, object?>? _valuesAtRunStart;
    private int _runDepth;

    /// <summary>Raised (possibly off the UI thread) when a screen's value has changed after a direct user action, or
    /// when a run the user started has ended with a different value. Screens in the view listen for their own name and light up.
    /// A handler that throws is logged through <see cref="ErrorLog.ReportBackground"/> and the others still run.</summary>
    public event EventHandler<ScreenChangedEventArgs>? ScreenChanged;

    /// <summary>Every screen name this lab has reported so far, in the order of their first reports. The all-labs
    /// self-test compares them with the views' <c>ScreenName</c> values, so a typo cannot silently stop a screen lighting.</summary>
    public IReadOnlyList<string> ReportedScreens
    {
        get { lock (_screenLock) return _reportedScreens.ToArray(); }
    }

    /// <summary>
    /// Says that a screen now shows <paramref name="value"/>. The first report of a screen is only a baseline (labs report
    /// every screen once at construction or activation); Reset and a model change are learner actions, so a later report
    /// after them raises like any other. Later, it raises
    /// <see cref="ScreenChanged"/> only if the value differs from the last one reported and no run is active; during a run
    /// it only remembers the value. A collection is compared by its contents (a text fingerprint taken now, so pass a
    /// snapshot such as <c>ToArray()</c> if the collection can change on another thread). Safe to call from any thread.
    /// </summary>
    protected void ReportScreen(string screen, object? value)
    {
        object? current = Fingerprint(value);
        lock (_screenLock)
        {
            bool known = _lastScreenValues.TryGetValue(screen, out var last);
            if (!known) _reportedScreens.Add(screen);
            if (known && Equals(last, current)) return;
            _lastScreenValues[screen] = current;
            if (!known || _runDepth > 0) return;   // a baseline, or a run that raises at its end
        }
        Raise(screen);
    }

    /// <summary>
    /// Starts a run the user asked for (training, say). Disposing it raises once for each screen whose value at the end
    /// differs from its value at the start (a change that is undone stays dark). Runs may nest; only the outermost raises.
    /// </summary>
    protected IDisposable BeginRun()
    {
        lock (_screenLock)
        {
            if (_runDepth++ == 0) _valuesAtRunStart = new Dictionary<string, object?>(_lastScreenValues, StringComparer.Ordinal);
        }
        return new RunScope(this);
    }

    private void EndRun()
    {
        List<string> changed = [];
        lock (_screenLock)
        {
            if (--_runDepth > 0) return;
            foreach (var (screen, before) in _valuesAtRunStart!)
                if (!Equals(before, _lastScreenValues[screen])) changed.Add(screen);
            _valuesAtRunStart = null;
        }
        foreach (var screen in changed) Raise(screen);
    }

    private void Raise(string screen)
    {
        var handler = ScreenChanged;
        if (handler == null) return;
        var args = new ScreenChangedEventArgs(screen);
        foreach (var one in handler.GetInvocationList().Cast<EventHandler<ScreenChangedEventArgs>>())
        {
            try { one(this, args); }
            catch (Exception e) when (e is not OutOfMemoryException) { ErrorLog.ReportBackground(e); }
        }
    }

    private sealed class RunScope(LabViewModel owner) : IDisposable
    {
        private int _disposed;
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) owner.EndRun(); }
    }

    private static object? Fingerprint(object? value)
    {
        if (value is null or string || value is not IEnumerable items) return value;
        var sb = new StringBuilder("[");
        foreach (var item in items)
        {
            string text = Fingerprint(item) switch
            {
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                float f => f.ToString("R", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                var o => o?.ToString() ?? "null",
            };
            sb.Append(text.Length).Append(':').Append(text);   // length-prefixed, so ["a|b"] differs from ["a","b"]
        }
        return sb.Append(']').ToString();
    }
}

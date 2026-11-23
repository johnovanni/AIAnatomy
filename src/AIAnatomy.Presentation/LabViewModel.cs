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

    /// <summary>Called when the user leaves this lab. Labs cancel their background work here.</summary>
    public virtual void Deactivate() { }

    /// <summary>Called when the seed changes (labs that use randomness redo what depends on it).</summary>
    protected virtual void OnSeedEdited() { }

    partial void OnSeedChanged(int value) => OnSeedEdited();
}

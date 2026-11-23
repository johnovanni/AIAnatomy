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
}

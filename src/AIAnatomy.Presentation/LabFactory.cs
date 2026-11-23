using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation;

/// <summary>
/// Builds the view-model for a lab id; every lab is registered in the constructor with <see cref="Register"/>. Lesson
/// links inside labs go through the view's routed navigate event, so the factory needs no lesson list or navigate
/// callback (final review M3). Most labs are built afresh each time their lesson opens; the Train lab is built once
/// per factory (so once per window) and kept, so a run carries on while the user reads other lessons (R35).
/// </summary>
public sealed class LabFactory
{
    private readonly Dictionary<string, Func<LabViewModel>> _creators = new(StringComparer.Ordinal);

    /// <param name="libraryChanged">Called after a lab saves a model to the library, so the model picker can list it.</param>
    public LabFactory(IActiveModelService models, string contentFolder, Action? libraryChanged = null)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFolder);
        var fables = new Lazy<string>(() => File.ReadAllText(Path.Combine(contentFolder, "texts", "aesop-fables.txt")));
        Register(ComingLaterLabViewModel.Id, () => new ComingLaterLabViewModel());
        Register(RulesVsLearningLabViewModel.Id, () => new RulesVsLearningLabViewModel(() => fables.Value));
        Register(ArchitectureMapLabViewModel.Id, () => new ArchitectureMapLabViewModel(models));
        Register(TokensLabViewModel.Id, () => new TokensLabViewModel(() => fables.Value));
        Register(PositionLabViewModel.Id, () => new PositionLabViewModel(models));
        Register(EmbeddingsLabViewModel.Id, () => new EmbeddingsLabViewModel(models));
        Register(AttentionLabViewModel.Id, () => new AttentionLabViewModel(models));
        Register(MultiHeadLabViewModel.Id, () => new MultiHeadLabViewModel(models));
        Register(FeedForwardLabViewModel.Id, () => new FeedForwardLabViewModel(models, () => fables.Value));
        Register(ResidualLabViewModel.Id, () => new ResidualLabViewModel(models));
        Register(SamplingLabViewModel.Id, () => new SamplingLabViewModel(models));
        Register(LearningLabViewModel.Id, () => new LearningLabViewModel(models));
        Register(StepThroughLabViewModel.Id, () => new StepThroughLabViewModel(models));
        Register(ScaleLabViewModel.Id, () => new ScaleLabViewModel(models));
        TrainLabViewModel? train = null;
        Register(TrainLabViewModel.Id, () => train ??= new TrainLabViewModel(models, () => fables.Value, libraryChanged));
    }

    public void Register(string labId, Func<LabViewModel> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labId);
        ArgumentNullException.ThrowIfNull(create);
        _creators[labId] = create;
    }

    public bool IsRegistered(string labId) => labId != null && _creators.ContainsKey(labId);

    public LabViewModel Create(string labId) =>
        labId != null && _creators.TryGetValue(labId, out var create)
            ? create()
            : throw new ArgumentException($"There is no lab called '{labId}'.", nameof(labId));
}

using AIAnatomy.Lessons;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation;

/// <summary>
/// Builds the view-model for a lab id. Later tasks add their labs in the constructor with <see cref="Register"/>
/// (the models, lessons and navigate callback are what those labs will be given).
/// <c>navigate</c> is how a lab asks the app to jump to a lesson; MainViewModel creates the factory itself and
/// passes its own NavigateToLesson, so there is no construction cycle.
/// </summary>
public sealed class LabFactory
{
    private readonly Dictionary<string, Func<LabViewModel>> _creators = new(StringComparer.Ordinal);

    public LabFactory(IActiveModelService models, LessonCatalog lessons, Func<string, Task> navigate, string contentFolder)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(lessons);
        ArgumentNullException.ThrowIfNull(navigate);
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

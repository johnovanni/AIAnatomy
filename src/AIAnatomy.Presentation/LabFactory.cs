using AIAnatomy.Lessons;
using AIAnatomy.Presentation.IdeaMap;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation;

/// <summary>
/// Builds the view-model for a lab id; every lab is registered in the constructor with <see cref="Register"/>. Lesson
/// links inside labs go through the view's routed navigate event, so the factory needs no navigate callback (final
/// review M3); it takes the lesson catalog only because the family tree numbers its bubbles from it (read from the
/// content folder when not given). Most labs are built afresh each time their lesson opens; the Train lab is built once
/// per factory (so once per window) and kept, so a run carries on while the user reads other lessons (R35). The factory
/// also builds one <see cref="Services.FamilyModels"/> from the content folder, shared by the Part B labs, so each shipped
/// family network is read from disk once per window (each lab still gets its own copy of the network).
/// </summary>
public sealed class LabFactory
{
    private readonly Dictionary<string, Func<LabViewModel>> _creators = new(StringComparer.Ordinal);

    /// <param name="libraryChanged">Called after a lab saves a model to the library, so the model picker can list it.</param>
    /// <param name="lessons">The lesson catalog (the family tree numbers its bubbles from it); read from
    /// <paramref name="contentFolder"/> when the family tree first needs it if not given.</param>
    public LabFactory(IActiveModelService models, string contentFolder, Action? libraryChanged = null, LessonCatalog? lessons = null)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFolder);
        Family = new FamilyModels(contentFolder);
        var fables = new Lazy<string>(() => File.ReadAllText(Path.Combine(contentFolder, "texts", "aesop-fables.txt")));
        var catalog = new Lazy<LessonCatalog>(() => lessons ?? LessonCatalog.Load(contentFolder));
        var familyTree = new Lazy<FamilyTreeData>(() => FamilyTreeData.Load(Path.Combine(contentFolder, "family-tree.json")));
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
        Register(FamilyTreeLabViewModel.Id, () => new FamilyTreeLabViewModel(familyTree.Value, catalog.Value, new MsaglGraphLayout()));
        Register(ExpertSystemLabViewModel.Id, () => new ExpertSystemLabViewModel());
        Register(SearchLabViewModel.Id, () => new SearchLabViewModel());
        Register(ClassicMlLabViewModel.Id, () => new ClassicMlLabViewModel());
        Register(RewardLabViewModel.Id, () => new RewardLabViewModel());
        Register(MemoryNetworkLabViewModel.Id, () => new MemoryNetworkLabViewModel(Family, models, () => fables.Value));
        TrainLabViewModel? train = null;
        Register(TrainLabViewModel.Id, () => train ??= new TrainLabViewModel(models, () => fables.Value, libraryChanged));
    }

    /// <summary>The shipped family networks (memory, diffusion), loaded once per window; Part B labs take this.</summary>
    public FamilyModels Family { get; }

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

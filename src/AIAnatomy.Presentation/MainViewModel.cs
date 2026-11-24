using System.Collections.ObjectModel;
using AIAnatomy.Lessons;
using AIAnatomy.Lessons.Markdown;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Contents;
using AIAnatomy.Presentation.IdeaMap;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation;

/// <summary>A request to scroll the lesson page to a named place (a lesson branch's anchor). A new <see cref="Serial"/> on
/// every request, so asking for the same anchor twice still scrolls.</summary>
public sealed record AnchorRequest(string Anchor, int Serial);

public sealed partial class MainViewModel : ObservableObject
{
    private readonly LessonCatalog _lessons;
    private readonly IActiveModelService _models;
    private readonly IProgressService _progress;
    private readonly ConnectionList _connections;
    private readonly IdeaMapData _map;
    private int _anchorSerial;
    private bool _navigating;
    private bool _revertingModel;
    private TrainLabViewModel? _trainLab;
    private bool _trainEndedAway;

    public MainViewModel(LessonCatalog lessons, SourceList sources, IActiveModelService models, ISettingsService settings,
        IProgressService progress, ConnectionList connections, IdeaMapData map, IGraphLayout layout, string? contentFolder = null)
    {
        _lessons = lessons ?? throw new ArgumentNullException(nameof(lessons));
        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _map = map ?? throw new ArgumentNullException(nameof(map));
        Labs = new LabFactory(models, contentFolder ?? AppPaths.Content, RefreshModelEntries);
        TrainLessonTitle = lessons.All.FirstOrDefault(l => l.LabId == TrainLabViewModel.Id)?.Title ?? "Training a model";

        Contents = new ContentsTreeViewModel(lessons, progress);
        IdeaMap = new IdeaMapViewModel(lessons, connections, map, progress, settings, models, layout);
        IdeaMap.OpenLessonRequested += (_, id) => OpenLessonById(id);
        foreach (var l in lessons.InContentsOrder) Lessons.Add(l);
        SettingsPage = new SettingsViewModel(settings, progress);
        RefreshModelEntries();
        _models.Changed += (_, _) => OnActiveModelChanged();
        SettingsPage.Applied += (_, _) => OnPropertyChanged(nameof(UseStackedLayout));
        settings.SaveFailed += (_, _) =>
            StatusMessage = $"Your settings couldn't be saved: {settings.LastSaveProblem}. They'll apply until you close the app.";

        var first = lessons.InSection(Section.Map).FirstOrDefault();
        if (first != null) Show(first);
    }

    /// <summary>The numbered contents (sections, lessons, branches and their marks).</summary>
    public ContentsTreeViewModel Contents { get; }

    /// <summary>The idea map page.</summary>
    public IdeaMapViewModel IdeaMap { get; }

    /// <summary>Every lesson in contents order. Temporary: the old window's lesson list reads it until the new shell (Task 9).</summary>
    public ObservableCollection<Lesson> Lessons { get; } = new();
    public ObservableCollection<ModelEntry> ModelEntries { get; } = new();
    public ISettingsService Settings { get; }

    /// <summary>The body text size (in points) from which the explanation goes under the lab instead of beside it.</summary>
    public const double StackedLayoutFromSize = 18;

    /// <summary>True at large text sizes: three side-by-side columns would leave the lab too narrow, so the Explanation pane goes below the lab pane.</summary>
    public bool UseStackedLayout => Settings.Current.BodyFontSize >= StackedLayoutFromSize;
    public SourceList Sources { get; }

    /// <summary>The Settings page (text size and theme).</summary>
    public SettingsViewModel SettingsPage { get; }

    /// <summary>The lab registry; labs not registered here show as "coming later".</summary>
    public LabFactory Labs { get; }

    [ObservableProperty] private Lesson? selectedLesson;

    /// <summary>The page of the lesson that is open (also while the idea map, Sources or Settings cover it).</summary>
    [ObservableProperty] private LessonPageViewModel? currentPage;

    /// <summary>The place on the lesson page to scroll to, after <see cref="OpenBranch"/>. Null until one is asked for.</summary>
    [ObservableProperty] private AnchorRequest? anchorRequest;
    [ObservableProperty] private LabViewModel? currentLab;
    [ObservableProperty] private ModelEntry? selectedModelEntry;
    [ObservableProperty] private MarkdownDocument? explanation;
    [ObservableProperty] private MarkdownDocument? formula;
    [ObservableProperty] private MarkdownDocument? brainsAndModels;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingLesson))]
    private bool isShowingSources;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingLesson))]
    private bool isShowingSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingLesson))]
    private bool isShowingIdeaMap;

    /// <summary>True when the lesson (lab and explanation) is showing, not the idea map, Sources or Settings page.</summary>
    public bool IsShowingLesson => !IsShowingSources && !IsShowingSettings && !IsShowingIdeaMap;
    [ObservableProperty] private string? statusMessage;

    /// <summary>
    /// A silent line in the header about a Train lab run while the lab is out of sight (another lesson is open, or the Sources
    /// or Settings page covers it), such as "Training in the background: step 340 of 600." (R35), or how a run that ended
    /// meanwhile ended, or that it could not start. Null while the Train lab is on show.
    /// </summary>
    [ObservableProperty] private string? backgroundStatus;

    // Not halfway through Show (which updates the line itself at the end): a lesson change passes through a moment where
    // the old Train lab looks uncovered, which would wrongly count as having seen how its run ended.
    partial void OnIsShowingSourcesChanged(bool value) { if (!_navigating) UpdateBackgroundStatus(); }
    partial void OnIsShowingSettingsChanged(bool value) { if (!_navigating) UpdateBackgroundStatus(); }
    partial void OnIsShowingIdeaMapChanged(bool value) { if (!_navigating) UpdateBackgroundStatus(); }

    /// <summary>True when the kept Train lab is the lab on screen (not covered by Sources or Settings).</summary>
    private bool TrainLabInView => _trainLab != null && ReferenceEquals(_trainLab, CurrentLab) && IsShowingLesson;

    private string TrainLessonTitle { get; }

    /// <summary>What the status line says when a source's web page could not be opened (no browser set up, or blocked).</summary>
    public void ShowLinkNotOpened(string url) =>
        StatusMessage = $"The web page could not be opened from here. You can type its address into your web browser: {url}";

    /// <summary>Stops every lab's background work, the kept Train lab's too. The window calls this when it closes.</summary>
    public void Shutdown()
    {
        CurrentLab?.Deactivate();
        if (_trainLab != null && !ReferenceEquals(_trainLab, CurrentLab)) _trainLab.Deactivate();
    }

    /// <summary>Re-reads the library (after a save or delete) and selects the active model's entry.</summary>
    public void RefreshModelEntries()
    {
        _revertingModel = true;
        try
        {
            ModelEntries.Clear();
            foreach (var e in _models.Library.List()) ModelEntries.Add(e);
            SelectedModelEntry = ActiveEntry();
        }
        finally { _revertingModel = false; }
    }

    [RelayCommand]
    public Task NavigateToLesson(string id)
    {
        Show(_lessons.Get(id));
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void ShowSources()
    {
        IsShowingSources = true;   // the new page's flag first, so IsShowingLesson never flickers true between two pages
        IsShowingSettings = false;
        IsShowingIdeaMap = false;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        IsShowingSettings = true;
        IsShowingSources = false;
        IsShowingIdeaMap = false;
    }

    [RelayCommand]
    private void ShowIdeaMap()
    {
        IsShowingIdeaMap = true;
        IsShowingSources = false;
        IsShowingSettings = false;
    }

    /// <summary>Leaves the idea map, Sources or Settings page and shows the open lesson (next to the contents) again.</summary>
    [RelayCommand]
    private void ShowContents() => BackToLesson();

    /// <summary>Leaves the idea map, Sources or Settings page and shows the current lesson again.</summary>
    [RelayCommand]
    private void BackToLesson()
    {
        IsShowingSources = false;
        IsShowingSettings = false;
        IsShowingIdeaMap = false;
    }

    /// <summary>What a click on a lesson does: shows it, or (if it is already open) just goes back to it.</summary>
    [RelayCommand]
    private void OpenLesson(Lesson? lesson)
    {
        if (lesson != null) OpenLessonById(lesson.Id);
    }

    /// <summary>What Ctrl+1 to Ctrl+5 do: open the section's first lesson.</summary>
    [RelayCommand]
    private void ShowSection(Section section)
    {
        var first = _lessons.InSection(section).FirstOrDefault();
        if (first != null) OpenLessonById(first.Id);
    }

    /// <summary>Opens the lesson a branch belongs to (the open lesson if it has the branch) and asks the page to scroll to
    /// the branch's anchor.</summary>
    [RelayCommand]
    private void OpenBranch(LessonBranch? branch)
    {
        if (branch == null) return;
        var lesson = CurrentPage?.Lesson.Branches.Contains(branch) == true
            ? CurrentPage.Lesson
            : _lessons.All.FirstOrDefault(l => l.Branches.Contains(branch));
        if (lesson == null) return;
        OpenLessonById(lesson.Id);
        AnchorRequest = new AnchorRequest(branch.Anchor, ++_anchorSerial);
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void GoNext() => OpenLessonById(CurrentPage!.Next!.Id);

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => OpenLessonById(CurrentPage!.Previous!.Id);

    private bool CanGoNext() => CurrentPage?.Next != null;
    private bool CanGoBack() => CurrentPage?.Previous != null;

    /// <summary>What a click on a connection chip does: another lesson opens; the whole loop opens the idea map in Loop mode
    /// with nothing chosen; an idea that is only on the map opens the map with that idea chosen.</summary>
    [RelayCommand]
    private void OpenConnection(ConnectionChip? chip)
    {
        if (chip == null) return;
        if (chip.IsWholeLoop)
        {
            IdeaMap.Mode = MapMode.Loop;
            IdeaMap.ClearChoice();
        }
        else if (_lessons.All.Any(l => l.Id == chip.OtherId))
        {
            OpenLessonById(chip.OtherId);
            return;
        }
        else
        {
            if (!IdeaMap.Bubbles.Any(b => b.Id == chip.OtherId)) IdeaMap.Mode = MapMode.Loop;
            if (IdeaMap.Bubbles.Any(b => b.Id == chip.OtherId)) IdeaMap.Choose(chip.OtherId);
        }
        ShowIdeaMap();
    }

    /// <summary>Shows the lesson; if it is the one already open, only brings it back (its lab is not recreated).</summary>
    private void OpenLessonById(string id)
    {
        if (CurrentPage?.Lesson.Id == id) BackToLesson();
        else Show(_lessons.Get(id));
    }

    [RelayCommand]
    private void ResetLab() => CurrentLab?.Reset();

    partial void OnSelectedLessonChanged(Lesson? value)
    {
        if (_navigating || value == null) return;
        Show(value);
    }

    partial void OnSelectedModelEntryChanged(ModelEntry? value)
    {
        if (_revertingModel || value == null) return;
        try
        {
            if (!IsActive(value)) _models.SetActive(value);   // throws for an unreadable entry
            StatusMessage = null;
        }
        catch (InvalidOperationException e)
        {
            StatusMessage = e.Message;
            _revertingModel = true;
            try { SelectedModelEntry = ActiveEntry(); }
            finally { _revertingModel = false; }
        }
    }

    // The active model may change from elsewhere (the Train lab's "Use in the labs"): keep the picker truthful.
    private void OnActiveModelChanged()
    {
        if (SelectedModelEntry == null || !IsActive(SelectedModelEntry)) RefreshModelEntries();
        CurrentLab?.OnActiveModelChanged();
        // The kept Train lab is not on show, but its "carry on from the active model" choice must stay true.
        if (_trainLab != null && !ReferenceEquals(_trainLab, CurrentLab)) _trainLab.OnActiveModelChanged();
    }

    // By file, not by name: a model saved over the active file is still the active entry (final review I10).
    private bool IsActive(ModelEntry e) => _models.IsActive(e);

    private ModelEntry? ActiveEntry() => ModelEntries.FirstOrDefault(e => e.IsReadable && IsActive(e));

    /// <summary>Shows a lesson, creating its lab once, and marks it opened.</summary>
    private void Show(Lesson lesson)
    {
        _navigating = true;
        try
        {
            AnchorRequest = null;   // a view bound later must not scroll to an old anchor
            IsShowingSources = false;
            IsShowingSettings = false;
            IsShowingIdeaMap = false;
            // A lab that keeps running (the Train lab, R35) is not stopped when the user leaves it.
            if (CurrentLab is { KeepsRunningWhenLeft: false } leaving) leaving.Deactivate();
            Explanation = lesson.Explanation;
            Formula = lesson.Formula;
            BrainsAndModels = lesson.BrainsAndModels;
            CurrentLab = Labs.IsRegistered(lesson.LabId) ? Labs.Create(lesson.LabId) : new ComingLaterLabViewModel();
            if (CurrentLab is TrainLabViewModel train && !ReferenceEquals(train, _trainLab)) WatchTrainLab(train);
            CurrentPage = new LessonPageViewModel(lesson, _lessons, CurrentLab, _connections, _map);
            SelectedLesson = lesson;
            GoNextCommand.NotifyCanExecuteChanged();
            GoBackCommand.NotifyCanExecuteChanged();
            Contents.Select(lesson.Id);
            _progress.MarkOpened(lesson.Id);
            UpdateBackgroundStatus();
        }
        finally { _navigating = false; }
    }

    private void WatchTrainLab(TrainLabViewModel train)
    {
        if (_trainLab != null) _trainLab.PropertyChanged -= OnTrainLabChanged;
        _trainLab = train;
        _trainEndedAway = false;
        train.PropertyChanged += OnTrainLabChanged;
    }

    private void OnTrainLabChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not TrainLabViewModel train) return;
        switch (e.PropertyName)
        {
            // A run that ended out of sight is reported once: finished, stopped, or (with no model yet) could not start.
            // Message comes after IsTraining when "Getting the model ready" fails, so both are watched.
            case nameof(TrainLabViewModel.IsTraining) or nameof(TrainLabViewModel.Message):
                if (!train.IsTraining && !train.IsPaused && (train.HasModel || train.Message != null) && !TrainLabInView) _trainEndedAway = true;
                break;
            case nameof(TrainLabViewModel.StepsDone) or nameof(TrainLabViewModel.TargetSteps) or nameof(TrainLabViewModel.IsPaused)
                or nameof(TrainLabViewModel.HasDiverged) or nameof(TrainLabViewModel.HasModel):
                break;
            default:
                return;
        }
        UpdateBackgroundStatus();
    }

    private void UpdateBackgroundStatus()
    {
        var t = _trainLab;
        string? text = null;
        if (TrainLabInView) _trainEndedAway = false;   // seen: nothing more to report
        else if (t != null)
        {
            string step = NumberText.Short(t.StepsDone), target = NumberText.Short(t.TargetSteps);
            if (t.IsTraining) text = t.TargetSteps == 0 ? "Training in the background: getting the model ready." : $"Training in the background: step {step} of {target}.";
            else if (t.IsPaused) text = $"Training is paused at step {step} of {target}. Open “{TrainLessonTitle}” to resume or stop it.";
            else if (_trainEndedAway && !t.HasModel && t.Message != null)
                text = $"Training could not start. Open “{TrainLessonTitle}” to see why.";
            else if (_trainEndedAway && t.HasModel)
                text = t.HasDiverged || t.Message != null
                    ? $"Training stopped in the background at step {step}. Open “{TrainLessonTitle}” to see why."
                    : $"Training finished in the background: {step} steps. Open “{TrainLessonTitle}” to save the model.";
        }
        BackgroundStatus = text;
    }
}

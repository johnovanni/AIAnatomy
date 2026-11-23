using System.Collections.ObjectModel;
using AIAnatomy.Lessons;
using AIAnatomy.Lessons.Markdown;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly LessonCatalog _lessons;
    private readonly IActiveModelService _models;
    private bool _navigating;
    private bool _revertingModel;

    public MainViewModel(LessonCatalog lessons, SourceList sources, IActiveModelService models, ISettingsService settings)
    {
        _lessons = lessons ?? throw new ArgumentNullException(nameof(lessons));
        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        // The factory is built here so it can be given our own NavigateToLesson (no construction cycle).
        Labs = new LabFactory(models, lessons, NavigateToLesson);

        Sections = Enum.GetValues<Section>();
        SettingsPage = new SettingsViewModel(settings);
        RefreshModelEntries();
        _models.Changed += (_, _) => OnActiveModelChanged();
        settings.SaveFailed += (_, _) =>
            StatusMessage = $"Your settings couldn't be saved: {settings.LastSaveProblem}. They'll apply until you close the app.";

        var first = lessons.InSection(Section.Map).FirstOrDefault();
        if (first != null) Show(first.Section, first);
    }

    public IReadOnlyList<Section> Sections { get; }
    public ObservableCollection<Lesson> Lessons { get; } = new();
    public ObservableCollection<ModelEntry> ModelEntries { get; } = new();
    public ISettingsService Settings { get; }
    public SourceList Sources { get; }

    /// <summary>The Settings page (text size and theme).</summary>
    public SettingsViewModel SettingsPage { get; }

    /// <summary>The lab registry; labs not registered here show as "coming later".</summary>
    public LabFactory Labs { get; }

    [ObservableProperty] private Section selectedSection;
    [ObservableProperty] private Lesson? selectedLesson;
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

    /// <summary>True when the lesson (lab and explanation) is showing, not the Sources or Settings page.</summary>
    public bool IsShowingLesson => !IsShowingSources && !IsShowingSettings;
    [ObservableProperty] private string? statusMessage;

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
        var lesson = _lessons.Get(id);
        Show(lesson.Section, lesson);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void ShowSources()
    {
        IsShowingSettings = false;
        IsShowingSources = true;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        IsShowingSources = false;
        IsShowingSettings = true;
    }

    /// <summary>Leaves the Sources or Settings page and shows the current lesson again.</summary>
    [RelayCommand]
    private void BackToLesson()
    {
        IsShowingSources = false;
        IsShowingSettings = false;
    }

    /// <summary>What a click on a lesson in the list does: shows it, or (if it is already selected) just goes back to it.</summary>
    [RelayCommand]
    private void OpenLesson(Lesson? lesson)
    {
        if (lesson == null) return;
        if (lesson == SelectedLesson) BackToLesson();
        else Show(lesson.Section, lesson);
    }

    /// <summary>What a click on a section tab (or Ctrl+1 to Ctrl+5) does: shows it, or goes back to its current lesson.</summary>
    [RelayCommand]
    private void ShowSection(Section section)
    {
        if (section == SelectedSection) BackToLesson();
        else SelectedSection = section;
    }

    [RelayCommand]
    private void ResetLab() => CurrentLab?.Reset();

    partial void OnSelectedSectionChanged(Section value)
    {
        if (_navigating) return;
        Show(value, _lessons.InSection(value).FirstOrDefault());
    }

    partial void OnSelectedLessonChanged(Lesson? value)
    {
        if (_navigating || value == null) return;
        Show(value.Section, value);
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
    }

    private bool IsActive(ModelEntry e) => e.Name == _models.Current.Info.Name && e.IsShipped == _models.Current.Info.IsShipped;

    private ModelEntry? ActiveEntry() => ModelEntries.FirstOrDefault(e => e.IsReadable && IsActive(e));

    /// <summary>Shows a section and one of its lessons, creating that lesson's lab once.</summary>
    private void Show(Section section, Lesson? lesson)
    {
        _navigating = true;
        try
        {
            SelectedSection = section;
            if (Lessons.Count == 0 || Lessons[0].Section != section)
            {
                Lessons.Clear();
                foreach (var l in _lessons.InSection(section)) Lessons.Add(l);
            }
            SelectedLesson = lesson;
            IsShowingSources = false;
            IsShowingSettings = false;
            Explanation = lesson?.Explanation;
            Formula = lesson?.Formula;
            BrainsAndModels = lesson?.BrainsAndModels;
            CurrentLab = lesson == null ? null : Labs.IsRegistered(lesson.LabId) ? Labs.Create(lesson.LabId) : new ComingLaterLabViewModel();
        }
        finally { _navigating = false; }
    }
}

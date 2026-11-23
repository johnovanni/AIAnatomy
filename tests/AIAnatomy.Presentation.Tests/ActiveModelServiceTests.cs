using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

public class ActiveModelServiceTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void ActiveModel_DefaultsToShippedFablesModel()
    {
        var service = _env.NewActiveModels();
        Assert.Equal("Fables model", service.Current.Info.Name);
        Assert.True(service.Current.Info.IsShipped);
    }

    [Fact]
    public void ActiveModel_SavedNameMissing_FallsBackToShipped()
    {
        var settings = _env.NewSettings();
        settings.Save(settings.Current with { ActiveModelName = "No such model" });
        Assert.Equal("Fables model", _env.NewActiveModels(settings).Current.Info.Name);
    }

    [Fact]
    public void ActiveModel_SavedNameKnown_IsUsed()
    {
        var settings = _env.NewSettings();
        settings.Save(settings.Current with { ActiveModelName = "Induction demo" });
        Assert.Equal("Induction demo", _env.NewActiveModels(settings).Current.Info.Name);
    }

    [Fact]
    public void ActiveModel_SavedFileUnreadable_FallsBackToShipped()
    {
        Directory.CreateDirectory(_env.UserModels);
        File.WriteAllText(Path.Combine(_env.UserModels, "broken.aimodel"), "not a model");
        var settings = _env.NewSettings();
        settings.Save(settings.Current with { ActiveModelName = "broken" });
        Assert.Equal("Fables model", _env.NewActiveModels(settings).Current.Info.Name);
    }

    [Fact]
    public void IsActive_ComparesTheFile_NotOnlyTheName()
    {
        // Final review I10: the active model is known by its file, so a model saved over that file is recognised.
        var service = _env.NewActiveModels();
        var fables = service.Library.List().Single(e => e.Name == "Fables model");
        Assert.True(service.IsActive(fables));
        Assert.Equal(Path.GetFullPath(fables.Path), Path.GetFullPath(service.ActivePath));
        Assert.False(service.IsActive(fables with { Path = Path.Combine(_env.UserModels, "Fables model.aimodel"), IsShipped = false }));

        var mine = service.Library.Save(service.Current, "a/b");
        service.SetActive(mine);
        Assert.True(service.IsActive(new AIAnatomy.Models.Language.ModelEntry("a_b", mine.Path.ToUpperInvariant(), false, true, null)));
        Assert.False(service.IsActive(fables));
    }

    [Fact]
    public void ActiveModel_SetActive_RaisesChanged_AndRemembersName()
    {
        var settings = _env.NewSettings();
        var service = _env.NewActiveModels(settings);
        var induction = service.Library.List().Single(e => e.Name == "Induction demo");
        int raised = 0;
        service.Changed += (_, _) => raised++;

        service.SetActive(induction);

        Assert.Equal(1, raised);
        Assert.Equal("Induction demo", service.Current.Info.Name);
        Assert.Equal("Induction demo", settings.Current.ActiveModelName);
        Assert.Equal("Induction demo", new SettingsService(_env.SettingsFile).Current.ActiveModelName);
    }

    [Fact]
    public void ActiveModel_SetActive_UnreadableEntry_Throws_AndChangesNothing()
    {
        Directory.CreateDirectory(_env.UserModels);
        File.WriteAllText(Path.Combine(_env.UserModels, "broken.aimodel"), "not a model");
        var service = _env.NewActiveModels();
        var broken = service.Library.List().Single(e => !e.IsReadable);
        int raised = 0;
        service.Changed += (_, _) => raised++;

        Assert.Throws<InvalidOperationException>(() => service.SetActive(broken));

        Assert.Equal(0, raised);
        Assert.Equal("Fables model", service.Current.Info.Name);
    }

    [Fact]
    public void ActiveModel_NoShippedModels_ThrowsPlainMessage()
    {
        var empty = new AIAnatomy.Models.Language.ModelLibrary(Path.Combine(_env.Root, "none"), _env.UserModels);
        var e = Assert.Throws<InvalidOperationException>(() => new ActiveModelService(empty, _env.NewSettings()));
        Assert.Contains("Fables model", e.Message);
    }
}

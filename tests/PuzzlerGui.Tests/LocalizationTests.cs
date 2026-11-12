using PuzzlerGui.Services;
using PuzzlerGui.ViewModels;

namespace PuzzlerGui.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void AllSupportedLanguages_ShareEnglishKeySet()
    {
        var service = LocalizationService.Instance;
        var englishKeys = LocalizationService.EnglishKeys;

        foreach (var language in new[] { "en", "zh-CN", "zh-TW", "ja" })
        {
            service.SetLanguage(language);
            Assert.Equal(englishKeys, service.Strings.Keys.ToHashSet());
            Assert.All(service.Strings.Values, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        }
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-TW", "zh-TW")]
    [InlineData("ja", "ja")]
    public void Apply_KnownLanguage_IsPreserved(string requested, string expected)
    {
        var service = LocalizationService.Instance;
        service.Apply(requested);
        Assert.Equal(expected, service.CurrentLanguage);
    }

    [Theory]
    [InlineData("xx-XX")]
    [InlineData("")]
    [InlineData(null)]
    public void Apply_UnsupportedLanguage_FallsBackToDetectedLanguage(string? requested)
    {
        var service = LocalizationService.Instance;
        service.Apply(requested);
        Assert.Contains(service.CurrentLanguage, new[] { "en", "zh-CN", "zh-TW", "ja" });
    }

    [Fact]
    public void SetLanguage_RefreshesViewModelLocalizedBindings()
    {
        var service = LocalizationService.Instance;
        service.SetLanguage("en");
        var viewModel = new MainWindowViewModel(new AppSettings { Language = "en" });
        var notified = new HashSet<string?>();
        viewModel.PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        service.SetLanguage("zh-CN");

        Assert.Equal("zh-CN", service.CurrentLanguage);
        Assert.Equal(service.Strings["puzzle.section"], viewModel.Strings["puzzle.section"]);
        Assert.Contains(nameof(MainWindowViewModel.Strings), notified);
        Assert.Contains("Item[]", notified);
        Assert.Contains(nameof(MainWindowViewModel.BoardSummary), notified);
        Assert.Contains(nameof(MainWindowViewModel.SolverStatusText), notified);
        Assert.Equal(service.Strings["tri.inherit"], viewModel.TriStateOptions[0].Display);

        service.SetLanguage("en");
    }

    [Fact]
    public void Settings_PersistsLanguagePreference()
    {
        var settings = new AppSettings { Language = "ja" };
        Assert.Equal("ja", settings.Language);
    }
}

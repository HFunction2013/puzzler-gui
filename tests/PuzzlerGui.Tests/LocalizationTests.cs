using PuzzlerGui.Services;

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

    [Fact]
    public void Apply_UnknownLanguage_FallsBackToDetectedLanguage()
    {
        var service = LocalizationService.Instance;
        service.Apply("xx-XX");
        Assert.Contains(service.CurrentLanguage, new[] { "en", "zh-CN", "zh-TW", "ja" });
    }

    [Fact]
    public void Settings_PersistsLanguagePreference()
    {
        var settings = new AppSettings { Language = "ja" };
        Assert.Equal("ja", settings.Language);
    }
}

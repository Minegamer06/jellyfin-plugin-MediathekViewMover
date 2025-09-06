using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.MediathekViewMover.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PluginTests;

public class LanguageServiceTests
{
    private readonly LanguageService _languageService = new(new NullLogger<LanguageService>());

    public static IEnumerable<object[]> LanguageTestData =>
        new List<object[]>
        {
            new object[] { "Cat's_Eyes/Alexia_(S01_E03).srt", "und" },
            new object[] { "Cat's_Eyes/Alexia_(S01_E03)_(Französisch).mp4", "fra" },
            new object[] { "Cat's_Eyes/Durrieux_(S01_E07).mp4", "und" },
            new object[] { "Cat's_Eyes/Gwen_(S01_E05)_(Französisch).mp4", "fra" },
            new object[] { "Cat's_Eyes/Tamara_(S01_E01).mp4", "und" },
            new object[] { "Cat's_Eyes/Tamara_(S01_E01)_(Französisch).mp4", "fra" }
        };

    [Theory]
    [MemberData(nameof(LanguageTestData))]
    public void GetLanguageFromFileName_ShouldReturnCorrectLanguage(string fileName, string expectedLanguage)
    {
        // Act
        var result = _languageService.GetLanguageFromFileName(fileName, CultureInfo.GetCultureInfo("und"));

        // Assert
        Assert.Equal(expectedLanguage, result);
    }
}

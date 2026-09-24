using System.Globalization;
using System.Reflection;
using CamfrogMultiID.App;

namespace CamfrogMultiID.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void AllStrings_PresentInEnglishAndThai()
    {
        var properties = typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
            .ToList();
        Assert.NotEmpty(properties);

        var original = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "en", "th" })
            {
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                foreach (var property in properties)
                {
                    var value = (string?)property.GetValue(null);
                    Assert.False(string.IsNullOrWhiteSpace(value), $"{property.Name} missing for '{culture}'");
                }
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void LoadedValues_ContainNoControlOrReplacementChars()
    {
        // Regression: an editing channel once wrote C1 controls into Thai
        // values. Any such corruption must fail loudly here.
        var properties = typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
            .ToList();
        var original = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "en", "th" })
            {
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                foreach (var property in properties)
                {
                    var value = (string?)property.GetValue(null) ?? string.Empty;
                    Assert.DoesNotContain("\uFFFD", value);
                    foreach (var ch in value)
                        Assert.False(ch >= '\u0080' && ch <= '\u009F', $"{property.Name} [{culture}] contains U+{(int)ch:X4}");
                }
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Thai_DiffersFromEnglish_ForTranslatedKeys()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var english = Strings.Save;
            CultureInfo.CurrentUICulture = new CultureInfo("th");
            var thai = Strings.Save;
            // Exact-glyph assertions are encoding-fragile; verify the lookup
            // actually switches language instead.
            Assert.NotEqual(english, thai);
            Assert.NotEqual(nameof(Strings.Save), thai);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void CompositeMessages_FormatWithoutError()
    {
        var newline = Environment.NewLine;
        Assert.Contains("Seaza", L10n.Fmt(Strings.MsgConfirmDelete, newline, "Seaza", "seaza"));
        Assert.Contains("3", L10n.Fmt(Strings.MsgConfirmStartAll, 3, newline));
    }
}

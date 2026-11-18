using Jellyfin.Plugin.Audience;
using Jellyfin.Plugin.Audience.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using Xunit;

public class AudienceRulesTests
{
    [Theory]
    [InlineData(" gp ", "PG")]
    [InlineData("m18", "TV-MA")]
    [InlineData("TP", "TV-G")]
    [InlineData("PASSED", "Approved")]
    [InlineData("PG", null)]
    [InlineData(null, null)]
    public void RemapsOnlyKnownRatings(string? input, string? expected) =>
        Assert.Equal(expected, AudienceRules.RemapRating(input));

    [Theory]
    [InlineData(null, true)]
    [InlineData(" NR ", true)]
    [InlineData("Not Rated", true)]
    [InlineData("unknown", false)]
    public void RecognizesUnratedValues(string? input, bool expected) =>
        Assert.Equal(expected, AudienceRules.IsUnratedValue(input));

    [Theory]
    [InlineData(14, 6.0f, true)]
    [InlineData(15, 9.0f, false)]
    [InlineData(14, 5.9f, false)]
    [InlineData(14, null, false)]
    [InlineData(null, 6.0f, true)]
    [InlineData(null, 5.9f, false)]
    [InlineData(null, null, false)]
    public void RatingAndCommunityThresholdsAreEnforced(int? score, float? community, bool expected)
    {
        var item = new Movie { Genres = ["Drama"], CommunityRating = community };
        Assert.Equal(expected, AudienceRules.WantsTag(item, score, new PluginConfiguration()));
    }

    [Theory]
    [InlineData(new[] { "Drama", "horror" }, false)]
    [InlineData(new[] { "Unknown" }, false)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "drama" }, true)]
    public void GenreRequiresGentleAndExcludesHarsh(string[] genres, bool expected)
    {
        var item = new Movie { Genres = genres, CommunityRating = 8 };
        Assert.Equal(expected, AudienceRules.WantsTag(item, 10, new PluginConfiguration()));
    }
}

using System.Collections.Frozen;
using Jellyfin.Plugin.Audience.Configuration;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Audience;

/// <summary>
/// The selection and rating rules, kept free of Jellyfin service dependencies so they
/// can be reasoned about and tested on their own.
/// </summary>
public static class AudienceRules
{
    /// <summary>
    /// Ratings Jellyfin cannot parse, mapped to the US rating that means the same thing.
    /// </summary>
    /// <remarks>
    /// Every entry is a documented equivalence between national classification boards,
    /// NOT a judgement about content. Guessing a rating from genre or synopsis is refused
    /// on purpose: a wrong guess fails OPEN, which is the exact failure mode parental
    /// controls exist to prevent.
    ///
    /// The mapping cuts both ways. It hides adult Singapore-rated titles that were
    /// previously visible to every account, and it rescues genuinely all-ages titles
    /// (Spain's TP, Brazil's L) that would otherwise be blocked as unrated.
    /// </remarks>
    public static readonly FrozenDictionary<string, string> RatingRemap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GP"] = "PG",          // US MPAA 1970-72, the direct predecessor of PG
            ["PG13"] = "PG-13",     // plain typo for PG-13
            ["M"] = "TV-14",        // Australia, Mature, recommended 15+
            ["TP"] = "TV-G",        // Spain, Todos los Publicos, all ages
            ["L"] = "TV-G",         // Brazil, Livre, all ages
            ["B"] = "TV-PG",        // Mexico, 12+
            ["B-15"] = "TV-14",     // Mexico, 15+
            ["VM14"] = "TV-14",     // Italy, Vietato ai Minori di 14
            ["NC16"] = "TV-MA",     // Singapore, 16+
            ["M18"] = "TV-MA",      // Singapore, 18+
            ["R21"] = "TV-MA",      // Singapore, 21+
            ["PASSED"] = "Approved" // pre-1968 US Production Code seal
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Genres that disqualify an item outright.
    /// </summary>
    public static readonly FrozenSet<string> HarshGenres =
        new[]
        {
            "Horror", "Thriller", "War", "Science Fiction", "Sci-Fi & Fantasy",
            "Animation", "Anime", "Action", "Action & Adventure", "Crime",
            "Reality", "Talk", "News"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Genres an item must have at least one of to qualify.
    /// </summary>
    public static readonly FrozenSet<string> GentleGenres =
        new[]
        {
            "Comedy", "Drama", "Romance", "Family", "Music", "Western",
            "Mystery", "Documentary", "History", "Soap", "Kids"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Values that Jellyfin itself treats as "no rating".
    /// </summary>
    private static readonly FrozenSet<string> UnratedValues =
        new[] { "n/a", "unrated", "not rated", "nr" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the replacement rating for an unparseable value, or null to leave it alone.
    /// </summary>
    public static string? RemapRating(string? officialRating)
    {
        if (string.IsNullOrWhiteSpace(officialRating))
        {
            return null;
        }

        return RatingRemap.TryGetValue(officialRating.Trim(), out var mapped)
            ? mapped
            : null;
    }

    /// <summary>
    /// Returns true when the value is one Jellyfin resolves to "unrated".
    /// </summary>
    public static bool IsUnratedValue(string? officialRating)
        => string.IsNullOrWhiteSpace(officialRating)
           || UnratedValues.Contains(officialRating.Trim());

    /// <summary>
    /// Decides whether an item belongs in the reduced library.
    /// </summary>
    /// <param name="item">The item under test.</param>
    /// <param name="ratingScore">
    /// The score from Jellyfin's localisation manager, or null when the rating is absent
    /// or unrecognised. Passed in rather than resolved here so this stays dependency-free.
    /// </param>
    /// <param name="config">Plugin settings.</param>
    public static bool WantsTag(BaseItem item, int? ratingScore, PluginConfiguration config)
    {
        var genres = item.Genres;
        if (genres is null || genres.Length == 0)
        {
            return false;
        }

        foreach (var genre in genres)
        {
            if (HarshGenres.Contains(genre))
            {
                return false;
            }
        }

        var hasGentle = false;
        foreach (var genre in genres)
        {
            if (GentleGenres.Contains(genre))
            {
                hasGentle = true;
                break;
            }
        }

        if (!hasGentle)
        {
            return false;
        }

        // A usable rating must clear the ceiling AND the quality floor. The floor matters
        // as much as the ceiling: the point of this tag is to make an overwhelming library
        // smaller, and a poorly reviewed film that happens to be rated PG adds noise rather
        // than removing it. An item with no community rating at all fails here, which is
        // deliberate - without a rating and without a score there is nothing to judge on.
        if (ratingScore.HasValue)
        {
            return ratingScore.Value <= config.MaxRatingScore
                   && item.CommunityRating.HasValue
                   && item.CommunityRating.Value >= config.MinCommunityRating;
        }

        // No usable rating: fall back to reputation alone, against its own floor. This is
        // what keeps the older classics, which are exactly what this audience wants and
        // which TMDB frequently has no US certification for. The floor is separate because
        // an unrated item is carrying more risk, so it may warrant a higher bar.
        return item.CommunityRating.HasValue
               && item.CommunityRating.Value >= config.MinCommunityRatingWhenUnrated;
    }
}

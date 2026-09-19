using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Audience.Configuration;

/// <summary>
/// Settings for the Audience plugin. Defaults reproduce the selection that was
/// applied by hand to this library: 561 of 2778 titles tagged for a viewer who
/// finds the full library overwhelming.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the scheduled task does anything at all.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether ratings Jellyfin cannot parse are rewritten
    /// to their US equivalent.
    /// </summary>
    /// <remarks>
    /// This is the safety-relevant half. A rating string Jellyfin cannot parse takes the
    /// same code path as "unrated", which means it stays VISIBLE to every profile,
    /// including one with an age ceiling set.
    /// </remarks>
    public bool RepairRatings { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the audience tag is applied to new content.
    /// </summary>
    public bool ApplyTags { get; set; } = true;

    /// <summary>
    /// Gets or sets the tag applied to selected items. This is the string that goes in the
    /// target user's "Allowed tags" list.
    /// </summary>
    /// <remarks>
    /// Lower case with no spaces is safest: Jellyfin normalises tags through
    /// GetCleanValue() and then compares with StringComparer.Ordinal.
    /// </remarks>
    public string TagName { get; set; } = "grandma";

    /// <summary>
    /// Gets or sets the highest parental-rating score an item may carry and still be tagged.
    /// </summary>
    /// <remarks>
    /// Scores come from Jellyfin's own rating table, so they follow the server's
    /// metadata country. For US: G/TV-G/TV-Y = 0, TV-Y7 = 7, PG/TV-PG = 10,
    /// PG-13 = 13, TV-14 = 14, R/NC-17/TV-MA = 17. Note TV-PG was rescored from
    /// 13 to 10 in the 10.11 rating rewrite.
    /// </remarks>
    public int MaxRatingScore { get; set; } = 14;

    /// <summary>
    /// Gets or sets the community rating an item must reach to be tagged when it DOES have a
    /// usable official rating. The ceiling alone is not enough: this tag exists to make an
    /// overwhelming library smaller, and a poorly reviewed film that happens to be rated PG
    /// adds noise rather than removing it. An item carrying no community rating at all fails
    /// this check, which is deliberate.
    /// </summary>
    public double MinCommunityRating { get; set; } = 6.0;

    /// <summary>
    /// Gets or sets the minimum community rating for an item with no usable parental rating.
    /// </summary>
    /// <remarks>
    /// Unrated items are admitted only on reputation, because there is no rating to judge
    /// them by. This is deliberately not a safety gate: a viewer given an allow-list is an
    /// adult, and child profiles are protected by MaxParentalRating plus BlockUnratedItems
    /// instead. Be aware this floor is weak on obscure titles, where a perfect score can
    /// rest on a handful of votes.
    /// </remarks>
    public double MinCommunityRatingWhenUnrated { get; set; } = 6.0;

    /// <summary>
    /// Gets or sets a value indicating whether an item untagged by hand is left alone.
    /// </summary>
    /// <remarks>
    /// The task keeps a ledger of every id it has ever tagged. If an id is in the ledger
    /// but no longer carries the tag, a human removed it on purpose. Without this, every
    /// nightly run would silently undo curation.
    /// </remarks>
    public bool RespectManualUntag { get; set; } = true;
}

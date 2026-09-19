using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Audience;

/// <summary>
/// Nightly maintenance: repair unparseable parental ratings, then keep the audience
/// tag current as new content arrives.
/// </summary>
/// <remarks>
/// Running in-process is the whole point. The REST equivalent (POST /Items/{id}) carries
/// three traps that do not exist here:
///   1. Name, Overview, ProductionYear, OfficialRating and CustomRating are assigned
///      unconditionally, so omitting one WIPES it.
///   2. IsLocked = request.LockData ?? false, so omitting LockData silently unlocks.
///   3. TrickplayInfoDto serializes but does not deserialize in 12.0, so a read-modify-write
///      round trip returns HTTP 500 for any item with trickplay thumbnails. The signature is
///      misleading: every Movie fails while every Series succeeds, which reads like a
///      permissions problem and is not.
/// Mutating the entity and calling UpdateToRepositoryAsync touches only what was changed,
/// and needs no API key, no drop-file and no container.
/// </remarks>
public class AudienceTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILocalizationManager _localization;
    private readonly ILogger<AudienceTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudienceTask"/> class.
    /// </summary>
    public AudienceTask(
        ILibraryManager libraryManager,
        ILocalizationManager localization,
        ILogger<AudienceTask> logger)
    {
        _libraryManager = libraryManager;
        _localization = localization;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Repair ratings and apply audience tags";

    /// <inheritdoc />
    public string Key => "AudienceMaintain";

    /// <inheritdoc />
    public string Description =>
        "Rewrites parental ratings Jellyfin cannot parse to their US equivalent, then " +
        "applies the audience tag to newly added titles that match the selection rules.";

    /// <inheritdoc />
    public string Category => "Audience";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(5).Ticks
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            _logger.LogInformation("Audience task is disabled; nothing to do.");
            return;
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
            IsVirtualItem = false,
            Recursive = true
        });

        _logger.LogInformation("Audience: examining {Count} titles.", items.Count);

        var ledger = LedgerStore.Load();
        var repaired = 0;
        var tagged = 0;
        var unmapped = new List<string>();
        var processed = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var changed = false;

            if (config.RepairRatings)
            {
                var remapped = AudienceRules.RemapRating(item.OfficialRating);
                if (remapped is not null)
                {
                    _logger.LogInformation(
                        "Audience: rating {Old} -> {New} on {Title}.",
                        item.OfficialRating,
                        remapped,
                        item.Name);

                    item.OfficialRating = remapped;

                    // Lock it so a later provider refresh cannot reintroduce the
                    // unparseable value. Nothing is forfeited: the provider had no
                    // usable US rating for these titles in the first place.
                    if (!item.LockedFields.Contains(MetadataField.OfficialRating))
                    {
                        item.LockedFields = item.LockedFields
                            .Append(MetadataField.OfficialRating)
                            .Distinct()
                            .ToArray();
                    }

                    repaired++;
                    changed = true;
                }
                else if (!AudienceRules.IsUnratedValue(item.OfficialRating)
                         && ResolveScore(item.OfficialRating) is null)
                {
                    // A rating that is neither recognised nor in the remap table. Report it
                    // rather than guess: it is currently taking the unrated path and is
                    // therefore visible to every profile.
                    unmapped.Add($"{item.OfficialRating} ({item.Name})");
                }
            }

            if (config.ApplyTags)
            {
                var hasTag = item.Tags.Contains(config.TagName, StringComparer.OrdinalIgnoreCase);
                var wants = AudienceRules.WantsTag(item, ResolveScore(item.OfficialRating), config);
                var seenBefore = ledger.Contains(item.Id);

                // An id in the ledger without the tag was untagged by a human on purpose.
                // Re-applying it would quietly undo their curation every single night.
                var suppressed = config.RespectManualUntag && seenBefore && !hasTag;

                if (wants && !hasTag && !suppressed)
                {
                    item.Tags = item.Tags.Append(config.TagName).ToArray();
                    ledger.Add(item.Id);
                    tagged++;
                    changed = true;
                }
            }

            if (changed)
            {
                await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken)
                    .ConfigureAwait(false);
            }

            processed++;
            progress.Report(processed * 100.0 / items.Count);
        }

        LedgerStore.Save(ledger);

        _logger.LogInformation(
            "Audience: repaired {Repaired} ratings, applied {Tagged} tags.",
            repaired,
            tagged);

        if (unmapped.Count > 0)
        {
            // Deliberately a warning. These are invisible to parental controls until a
            // human adds a mapping, so they must not scroll past as information.
            _logger.LogWarning(
                "Audience: {Count} rating value(s) are neither recognised by Jellyfin nor " +
                "in the remap table, so they resolve as unrated and stay visible to every " +
                "profile: {Values}",
                unmapped.Count,
                string.Join("; ", unmapped.Distinct()));
        }
    }

    /// <summary>
    /// Resolves a rating string to Jellyfin's numeric score, or null when it does not parse.
    /// </summary>
    private int? ResolveScore(string? officialRating)
    {
        if (string.IsNullOrWhiteSpace(officialRating))
        {
            return null;
        }

        // Jellyfin 10.11 replaced the flat rating table (us.csv, string -> int) with a scored one
        // (us.json), and GetRatingLevel went with it. GetRatingScore returns a ParentalRatingScore
        // carrying Score plus SubScore; SubScore is what separates R (17/0) from NC-17 and TV-MA
        // (17/1). Only Score is needed here, because the tag rule is a plain ceiling.
        return _localization.GetRatingScore(officialRating)?.Score;
    }
}

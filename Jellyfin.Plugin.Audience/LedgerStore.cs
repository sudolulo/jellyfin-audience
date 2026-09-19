using System.Globalization;

namespace Jellyfin.Plugin.Audience;

/// <summary>
/// Remembers every item this plugin has ever tagged.
/// </summary>
/// <remarks>
/// This is what makes manual curation stick. The selection rules are a guess; the person
/// reviewing the result will remove titles that do not belong. Without a record of what was
/// tagged, the next run cannot tell "never seen, should tag" apart from "tagged once, human
/// removed it", and would re-apply the tag every night forever.
/// </remarks>
public static class LedgerStore
{
    private const string FileName = "tagged-ids.txt";

    private static string LedgerPath =>
        Path.Combine(
            Plugin.Instance?.DataFolderPath
                ?? throw new InvalidOperationException("Plugin instance is not initialised."),
            FileName);

    /// <summary>
    /// Loads the set of previously tagged item ids. Returns an empty set on first run.
    /// </summary>
    public static HashSet<Guid> Load()
    {
        var result = new HashSet<Guid>();
        var path = LedgerPath;

        if (!File.Exists(path))
        {
            return result;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (Guid.TryParse(trimmed, out var id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    /// Writes the ledger atomically, so an interrupted run cannot truncate it.
    /// </summary>
    /// <remarks>
    /// A truncated ledger is not a cosmetic problem: every id lost from it becomes an item
    /// the next run believes it has never seen, which silently re-applies tags a human
    /// deliberately removed.
    /// </remarks>
    public static void Save(HashSet<Guid> ids)
    {
        var path = LedgerPath;
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + ".tmp";
        File.WriteAllLines(
            temp,
            ids.Select(id => id.ToString("N", CultureInfo.InvariantCulture)));
        File.Move(temp, path, overwrite: true);
    }
}

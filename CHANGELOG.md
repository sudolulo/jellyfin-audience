# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-19

### Added

- Rating repair. A rating string Jellyfin cannot parse is not merely untidy: it
  takes the same code path as "unrated", which stays visible to every profile
  including one with an age ceiling set. Foreign board ratings are remapped to
  their documented US equivalents (GP to PG, TP and L to TV-G, B to TV-PG, B-15
  and VM14 and M to TV-14, NC16 and M18 and R21 to TV-MA, Passed to Approved,
  and the plain typo PG13 to PG-13). The repaired value is added to the item's
  locked fields so a later provider refresh cannot undo it.

- Ratings are never inferred from genre, synopsis or any other content signal.
  A wrong guess fails open, which is the single failure mode parental controls
  exist to prevent. Anything unmappable is reported as a warning for a human to
  map, and left alone.

- Audience tagging. Applies a configurable tag (default `grandma`) to titles
  that pass a genre and rating rule, so a user account can be narrowed with
  Allowed Tags. Because Allowed Tags fails closed, new content is invisible to
  that account until tagged, which is why this runs on a schedule rather than
  once.

- A curation ledger at `tagged-ids.txt` in the plugin data folder. An item whose
  id is in the ledger but which no longer carries the tag was untagged by a
  person on purpose, and is never tagged again. Without it every run would
  silently undo manual curation.

- Runs as a Jellyfin scheduled task, daily at 05:00 by default, configurable
  from the Scheduled Tasks screen.

[1.0.0]: https://git.arch.fyi/flan/jellyfin-audience/releases/tag/v1.0.0

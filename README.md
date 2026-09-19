# Jellyfin Audience

A Jellyfin plugin that repairs ratings the server cannot parse, and maintains an
audience tag so a user account can be narrowed to a manageable subset of a large
library.

It runs in-process as a scheduled task. It needs no API key, no credential file
and no external service.

## Why it exists

### Ratings Jellyfin cannot parse are a hole in parental controls

Jellyfin resolves an `OfficialRating` string to a numeric score. When it cannot,
the item takes the same path as an unrated one, and unrated items are **visible
to everyone by default** unless an account explicitly blocks unrated content.

That is not a theoretical problem. A real library scanned for this contained
Singapore board ratings (`M18`, `NC16`, `R21`) on adult titles. Jellyfin could
not parse any of them, so no age ceiling would have hidden them. It cuts the
other way too: `TP` (Spain) and `L` (Brazil) both mean "all ages", and titles
carrying them were being hidden from children as though unrated.

This plugin remaps those strings to their documented US equivalents and locks
the result.

### Allowed Tags fails closed

Narrowing an account with Allowed Tags is the right mechanism, but it is a
whitelist: anything without the tag is invisible. A library that keeps growing
would leave that account frozen at whatever was tagged on the day it was set up.
So the tagging pass has to run on a schedule, not once.

## What it does not do

It does not guess a rating from genre, synopsis, or anything else about the
content. A wrong guess fails open, and failing open is the one outcome parental
controls exist to prevent. Ratings it cannot map are logged as warnings and left
untouched, for a person to decide.

## Configuration

Dashboard, then Plugins, then Audience.

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | on | Master switch for the scheduled task |
| `RepairRatings` | on | Remap unparseable board ratings |
| `ApplyTags` | on | Maintain the audience tag |
| `TagName` | `grandma` | Tag applied to matching titles |
| `MaxRatingScore` | `14` | Rating ceiling for tagging. Inclusive |
| `MinCommunityRatingWhenUnrated` | `6.0` | Community-rating floor for titles with no usable rating |
| `RespectManualUntag` | on | Honour the ledger, so manual untagging sticks |

The US rating ladder is `0` G and TV-G, `7` TV-Y7, `10` PG and TV-PG, `13`
PG-13, `14` TV-14, `17` R and TV-MA. Note that Jellyfin 10.11 rescored `TV-PG`
from 13 to 10, so a ceiling carried over from an older install will not behave
the way it used to.

`MinCommunityRatingWhenUnrated` is a weak gate on obscure titles, where a
perfect score can rest on a handful of votes. It is a starting filter, not a
verdict.

## Curating the result

The rule is a first pass, not an opinion. To remove something, untag it in the
Jellyfin UI. The ledger records that the item was tagged once, so the next run
sees a deliberate removal and leaves it alone.

Turning off `RespectManualUntag` makes every run re-apply the rule, which undoes
curation. It exists for the case where you want to reset and start again.

## Building

Needs the .NET 9 SDK. On a machine where it is a user-local install rather than
a system package, it will not be on the PATH of a non-interactive shell:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
dotnet build Jellyfin.Plugin.Audience/Jellyfin.Plugin.Audience.csproj -c Release
```

The output is `Jellyfin.Plugin.Audience/bin/Release/net9.0/Jellyfin.Plugin.Audience.dll`.

## Installing

Copy the DLL and `meta.json` into a folder named `Audience_1.0.0.0` inside
Jellyfin's `plugins` directory, then restart the server. The plugin appears
under Dashboard, Plugins, and its task appears under Scheduled Tasks as
"Repair ratings and apply audience tags".

`targetAbi` is `10.11.0.0`. That is deliberate and is not a mistake on a 12.x
server: a plugin built against `Jellyfin.Controller` 10.11.11 loads and runs on
Jellyfin 12.0. Do not raise it without confirming the newer package exists and
the result still loads.

## Releasing

To publish through a Jellyfin plugin catalog, zip the DLL and `meta.json`, host
the zip, and add a repository manifest entry whose `sourceUrl` points at it. The
`sourceUrl` must end in `.zip`, and the `checksum` field must be the MD5 of the
actual published archive. No manifest is committed here, because a manifest
carrying a placeholder checksum or a URL with nothing behind it produces a
broken install for anyone who adds it.

## Licence

GPL-3.0.

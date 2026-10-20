# Native Language

A Jellyfin plugin that shows the **original title** for movies and series in the original languages you choose, while everything else keeps the title from your library's metadata language.

Jellyfin fetches every title in a library in one metadata language. If that language is English, a French film you know by its French name is listed under its English title, and if it is French, every English film gets a French title too. This plugin lets you keep one metadata language for the library and make an exception for the languages you'd rather see in their original form.

## Using it

1. Keep your library's **metadata language** set to the language you want for plots, genres and most titles (Dashboard → Libraries → your library).
2. In the plugin, choose a **mode** and list languages with the ISO 639-1 codes TMDb uses:

   | You want | Mode | Languages |
   |---|---|---|
   | Only films and series from your own country in their original language | Only these | e.g. `sv` (Swedish), `da` (Danish), `no` (Norwegian) |
   | A few languages you understand in their original form | Only these | e.g. `fr,de,es` |
   | Everything in its original language, except languages in other scripts | All except these | e.g. `ja,ko,zh,th,ar,he,ru` |
   | Everything in its original language | All except these | leave empty |

3. Every movie or series that matches gets its original title; everything else is left alone. Titles whose original language already is your library's metadata language are never touched.

## How it works

- Looks up each movie's or series' original language on TMDb (once; results are cached), with Jellyfin's own TMDb key unless you set your own.
- If it matches, sets the title to the original title and **locks** it, so metadata refreshes don't change it back. The sort title follows the new title.
- Runs by itself when a new item gets its metadata, and after every library scan. Run it by hand from **Dashboard → Scheduled Tasks → Apply native-language titles**.
- Episodes and seasons are not touched.

## Install

1. **Dashboard → Plugins → Repositories → +**
   - Name: `Native Language`
   - URL: `https://raw.githubusercontent.com/andersgar/jellyfin-plugin-native-language/main/manifest.json`
2. **Catalog → Native Language → Install**, then restart Jellyfin. Jellyfin picks the build that matches your version.
3. **Dashboard → Native Language** (in the side menu, or under My Plugins): choose a mode and languages. No TMDb key is needed: by default the plugin uses the same TMDb access as Jellyfin itself.

Tip: tick **Dry run** first and run the scheduled task. The log then lists what would change, without changing anything.

## Settings

| Setting | Default | |
|---|---|---|
| Enabled | on | |
| Own TMDb API key | empty | Empty uses Jellyfin's TMDb key (the one in the TMDb plugin's settings, else Jellyfin's built-in key). Enter your own v3 key or v4 read access token to override it. |
| Keep the original title for | Only these original languages | Or: All original languages except these |
| Languages | empty | ISO 639-1 codes, comma separated. `nb` and `nn` count as Norwegian (`no`). |
| Movies / Series | on / on | |
| Lock the title | on | Without the lock a metadata refresh can revert the title |
| Dry run | off | Only log what would change |

To undo a title, edit the item's metadata in Jellyfin, untick the locked *Name* field and refresh its metadata.

## Compatibility

| Jellyfin | Plugin build |
|---|---|
| 10.10 | `x.y.z.10` (net8.0) |
| 10.11 | `x.y.z.11` (net9.0) |
| 12.1 and newer | `x.y.z.121` (net10.0) |

All three are built by GitHub Actions on every `v*` tag, which also updates `manifest.json`. Jellyfin installs the build for its own version automatically.

## License

GPL-3.0, like Jellyfin.

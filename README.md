# Native Language

A Jellyfin plugin that shows the **original title** for movies and series in the original languages you choose, while everything else keeps the title from your library's metadata language.

Jellyfin fetches every title in a library in one metadata language. If that language is English, a French film you know by its French name is listed under its English title, and if it is French, every English film gets a French title too. This plugin lets you keep one metadata language for the library and make an exception for the languages you'd rather see in their original form.

## Using it

1. Keep your library's **metadata language** set to the language you want for most titles (Dashboard → Libraries → your library).
2. In the plugin, list the **original languages** whose titles should stay as they are. Use the ISO 639-1 codes that TMDb uses:

   | You want original titles for | Languages |
   |---|---|
   | Norwegian | `no,nb,nn` |
   | Swedish | `sv` |
   | Danish | `da` |
   | German | `de` |
   | French | `fr` |
   | Several, e.g. all Nordic | `no,nb,nn,sv,da,fi,is` |

3. Every movie or series whose original language is on the list gets its original title; all others are left alone.

Some languages use their own script. With `ja`, `ko` or `zh` in the list, titles show in that script (for example Japanese kanji), which may not be what you want in a mostly Latin-script library.

## How it works

- Looks up each movie's or series' original language on TMDb (once; results are cached).
- If the language is on your list, sets the title to the original title and **locks** it, so metadata refreshes don't change it back. The sort title follows the new title.
- Runs by itself when a new item gets its metadata, and after every library scan. Run it by hand from **Dashboard → Scheduled Tasks → Apply native-language titles**.
- Episodes and seasons are not touched.

## Install

1. **Dashboard → Plugins → Repositories → +**
   - Name: `Native Language`
   - URL: `https://raw.githubusercontent.com/andersgar/jellyfin-plugin-native-language/main/manifest.json`
2. **Catalog → Native Language → Install**, then restart Jellyfin.
3. **My Plugins → Native Language**: paste a TMDb API key (themoviedb.org → Settings → API; a v3 key or a v4 read access token both work) and set your language list.

Tip: tick **Dry run** first and run the scheduled task. The log then lists what would change, without changing anything.

## Settings

| Setting | Default | |
|---|---|---|
| Enabled | on | |
| TMDb API key | – | Required |
| Original languages | `no,nb,nn` | ISO 639-1 codes, comma separated |
| Movies / Series | on / on | |
| Lock the title | on | Without the lock a metadata refresh can revert the title |
| Dry run | off | Only log what would change |

To undo a title, edit the item's metadata in Jellyfin, untick the locked *Name* field and refresh its metadata.

## Compatibility

Built against Jellyfin 12.1 (`net10.0`). Releases are built by GitHub Actions on every `v*` tag, which also updates `manifest.json`.

## License

GPL-3.0, like Jellyfin.

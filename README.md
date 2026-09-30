# SephiriaToolbox

**English** | [简体中文](README.zh-CN.md)

A DPS meter, combat statistics and inventory arrangement mod for *Sephiria*. Works on both Mac and Windows.

> Unofficial mod, not affiliated with the game's developer TEAMHORAY.

## Features

### Combat statistics

- Damage and average DPS for every player and every damage source (weapon moves, artifacts, magic, debuffs, …).
- Three tabs: Current for the area you are fighting in, History to page through finished areas floor by floor, and Total for the whole run.
- Details can be expanded by source or by damage type.

### Builds and records

- The Build tab shows every party member's weapon, artifacts, talents and stats.
- A record of every run is saved automatically and can be browsed in the Records tab:
  - Equipment, talents and stats
  - Hard Mode modifiers
  - Each floor's boss modifiers and defense
  - Run results
- Records are stored locally:
  - Mac: `~/Saved Games/Sephiria_DpsLogs`
  - Windows: `Documents\Saved Games\Sephiria_DpsLogs`

### Inventory arrangement

- A numerical model built from the game's source code, covering sets, artifacts, weapons, Miracles, Talents, debuffs, boss defense and more.
- Simulates every possible layout locally, finds the one that scores highest for your goal together with the best tablet level allocation, and rearranges the inventory automatically once you confirm.
- Options:
  - Goal: most levels, overall damage, weapon first, magic first, custom weights
  - Scenario: single target, multiple targets
- Artifact value table: how DPS changes if an artifact gains one more level, or is removed.
- Enhance advice: compares the weapons your current weapon can be enhanced into and recommends one (of little use if you already know your preset well).
- Tablet Imprinting advice: with the imprinting talent, shows which tablet is most worth imprinting. **Suggestions only — it never imprints a tablet by itself.**
- Calculations run on a background thread and do not block the game's main thread.

## Hotkeys

| Key | Action |
|---|---|
| F7 | Minimize / expand |
| F8 | Hide / show |
| F9 | Switch tab |
| F10 | Expand all details |
| F5 / F6 | Previous / next page (PgUp / PgDn also work on Windows) |

Top-right corner of the window: A- / A+ to scale, EN / 中 to switch the interface language, Lock to prevent accidental dragging.

## Language

The interface is available in English and Chinese. By default it follows the game's language (Chinese when the game is set to Chinese, English otherwise); the EN / 中 button switches it manually.

All interface text lives in the language packs `Lang/zh-CN.json` and `Lang/en-US.json`, which are built into the DLL; the code only refers to their keys. To fix a translation or add a language without rebuilding, put `lang/<language>.json` (for example `lang/en-US.json`) into the mod folder, using the same keys — its entries override the built-in pack, and a new file adds a new language to the switch button. Entries missing from a pack fall back to Chinese.

## Installation

1. Get the `SephiriaToolbox` folder, which contains `SephiriaToolbox.dll` and `metadata.json`. Download the released zip, or build it yourself (see the next section).
2. Put it into the game's `AddOns` folder (create the folder if it does not exist):
   - **Mac**: in Steam, right-click the game → Manage → Browse local files, then right-click `Sephiria.app` → Show Package Contents. Place it as `Sephiria.app/AddOns/SephiriaToolbox/`.
   - **Windows**: in the folder that contains `Sephiria.exe`, place it as `AddOns\SephiriaToolbox\`.
3. Restart the game. Mods are loaded when the game starts.

Built for game version 1.0.33. If the mod breaks after a game update, rebuilding it usually fixes it.

## Building

Requirements:

- .NET SDK (tested with .NET 10).
- An installed copy of the game. The build references the game's own DLLs, which are not included in this repository.

```bash
dotnet build -c Release -o out
```

By default, the game is looked up in Steam's default install location:

- Mac: `~/Library/Application Support/Steam/steamapps/common/Sephiria/Sephiria.app/Contents/Resources/Data/Managed`
- Windows: `C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data\Managed`

If the game is installed elsewhere, point the build to its `Managed` folder:

```bash
dotnet build -c Release -o out -p:SephiriaManaged="path/to/your/game/Managed"
```

After building, put `out/SephiriaToolbox.dll` together with `metadata.json` into `AddOns/SephiriaToolbox/`.

## Notes

- Everything is computed locally. The mod never connects to the internet and uploads no data.
- In multiplayer, damage is tracked for the whole party.

## License

[MIT](LICENSE)

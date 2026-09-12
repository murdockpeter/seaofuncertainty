# Production Asset and Provenance Ledger

Status: distribution inventory refreshed on **2026-09-11**. Any new or modified visual or audio asset must be added here before approval.

| Asset family | Current implementation | License / distribution basis |
|---|---|---|
| Models | Production procedural meshes and primitives in `OperationalMap3D.cs`: carrier, escorts, surface combatants, submarine, and aircraft | Project-authored code; all rights reserved by project author; no external geometry |
| Materials | Nine packaged `Command*.mat` files plus runtime formation, deck, canopy, foam, and atmosphere materials | Project-authored values using Unity Standard/Sprites shaders; Unity shader redistribution follows the Unity Editor EULA |
| Textures | Ocean, terrain, normal, and cloud textures generated deterministically at runtime; geographic ocean color zones sample the packaged ETOPO elevation grid | Project-authored algorithms; NOAA NCEI ETOPO 2022 attribution applies to the geographic depth/elevation input |
| VFX | Pooled lines, rings, primitives, persistent wakes, contrails, and weather streaks | Project-authored code; no external assets |
| Audio | Eight deterministic synthesized action cues and synthesized open-sea ambience in `OperationalMissionAudio.cs` | Project-authored synthesis code; no samples, generative model, or third-party recordings |
| Geography | Locally cropped Natural Earth 1:10m polygon mesh plus a two-arc-minute NOAA ETOPO 2022 elevation/bathymetry subset; logical terrain remains hex data | Natural Earth public domain; NOAA NCEI ETOPO 2022, doi:10.25921/fd45-gt74; retain attribution and “not for navigation” notice |

## File-level distribution inventory

| Shipped resource | Source and author | License / distribution basis | Status |
|---|---|---|---|
| `Assets/Resources/Geography/luzon-strait-coastline.json` | Natural Earth 1:10m land, cropped by the project coastline tool; embedded metadata records source cache and bounds | Natural Earth public domain; not for navigation | Approved with attribution |
| `Assets/Resources/Geography/luzon-strait-etopo-2022.bytes` and `luzon-strait-etopo-2022-metadata.json` | NOAA NCEI ETOPO 2022 15-arc-second global relief, sampled every eight source cells into a two-arc-minute theater subset by `tools/build-luzon-elevation.ps1` | United States government work; cite NOAA NCEI ETOPO 2022, doi:10.25921/fd45-gt74; not for navigation | Approved with attribution |
| `Assets/Resources/Materials/3D/CommandBlue.mat`, `CommandRed.mat`, `CommandContact.mat`, `CommandWarning.mat`, `CommandLine.mat`, `CommandWater.mat`, `CommandLand.mat`, `CommandLittoral.mat`, `CommandHighland.mat` | Authored in this repository by project code and editor tooling | Project author; Unity built-in shader redistribution | Approved |
| `Assets/Resources/UI/SeaTheme.uss` and `SeaRuntimePanelSettings.asset` | Authored in this repository | Project author | Approved |
| `Assets/Resources/UI/UnityDefaultRuntimeTheme.tss` | Unity Technologies runtime theme | Redistributable as part of a Unity-built player under the Unity Editor EULA | Approved only inside Unity player |
| `Assets/Resources/Data/scenario-catalog.json` | Authored in this repository; structured scenario and formation data | Project author | Approved |
| Formation meshes, distant symbols, recognition marks, wakes, Contacts, status cues, and action/rating glyphs | Generated at runtime from repository C# and USS | Project author; no external inputs | Approved |
| Ocean/terrain color, normal, bathymetry, haze, cloud, foam, and weather visuals | Generated at runtime from repository C#, scenario data, and the packaged ETOPO subset | Project-authored presentation; NOAA ETOPO attribution applies to measured elevation and depth-derived color zones | Approved with attribution |
| Eight action cues and open-sea ambience | Generated from deterministic oscillators and noise in repository C# | Project author; no recordings or samples | Approved |

## Geography processing record

The Luzon coastline resource is built from the local GCBH Natural Earth cache (`global-land.geojson`), cropped to the bounds embedded in the JSON, simplified, and packaged without altering authoritative hex terrain. Runtime verification currently loads 103 polygons and 719 off-map vertices.

The Luzon ETOPO resource is reproducibly downloaded from NOAA’s ERDDAP service by `tools/build-luzon-elevation.ps1`. It contains 72,800 signed elevation samples in a compact project binary: positive values drive curved land relief and negative values drive ocean depth coloration. Both geographic resources are presentation data, not navigational data, and neither changes authoritative hex terrain or rules.

## Retired assets

`tactical-archipelago-v1.png` was removed from distributable Resources on 2026-09-08. Its origin was not documented to production standard and the active UI no longer references it. No AI-generated raster image remains in the distributable asset tree.

## Release audit

Before every public build: enumerate `Assets/Resources`, compare it with this inventory, verify attribution survives packaging, reject undocumented binaries, and record the reviewer/date. Generated imagery must include prompt, tool/model, date, seed/reference inputs, and replacement status; the current build contains no generated imagery.

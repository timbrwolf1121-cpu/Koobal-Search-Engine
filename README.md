# Koobal Search Engine

**v1.0.0** — predictive suggestions for the stock VAB/SPH parts search bar (full functional release).

Type to get live matches for parts, stock filters (category, manufacturer, diameter, tags, modules, resources, tech), and mods/authors — then click a row to apply it. Recent searches appear when the field is empty. Hover a recent search and click **X** to remove just that item.

Attaches a dropdown under the native search field. Does not replace the stock parts UI. Large Koobal PartSearch results use a recycled icon pool so the list stays scrollable.

## Install

**CKAN (recommended):** search for `KoobalSearchEngine` and install. That also pulls **Harmony 2**.

Manual:

1. Close KSP.
2. Copy `GameData/KoobalSearchEngine/` into your KSP `GameData/` folder (merge/overwrite).
3. Launch KSP and open the VAB or SPH.
4. Confirm in `KSP.log`: `[Koobal] Koobal Search Engine v1.0.0 active.`

## Requirements

- **KSP 1.12.x** (tested on 1.12.5)
- **[Harmony 2](https://github.com/KSPModdingLibs/HarmonyKSP/releases)** — `GameData/000_Harmony/` must be present (CKAN id `Harmony2`)

Do **not** install alongside **Koobal Native Search** (or other mods that replace/hijack the same stock search bar).

## Downloads

- [SpaceDock](https://spacedock.info/mod/4394/Koobal+Search+Engine) — player zip (also listed on CKAN)
- [GitHub Releases](https://github.com/timbrwolf1121-cpu/Koobal-Search-Engine/releases) — optional mirror
- Unpacked source lives in [`Source/`](Source/). Courtesy snapshot zip (older): [`Source/KoobalSearchEngine_v0.8.5.3_SOURCE.zip`](Source/KoobalSearchEngine_v0.8.5.3_SOURCE.zip)

## License

MIT — see [LICENSE](LICENSE). Author: **timbrwolf1121**.

## Links

- [KSP forum](https://forum.kerbalspaceprogram.com/topic/231306-complete-overhaul-of-parts-search/)
- Issues: include your `KSP.log` and the query/steps to reproduce

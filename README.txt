# Koobal Search Engine — v0.8.5.0-beta

Predictive search for the KSP editor (VAB/SPH). Koobal attaches a Google-style
dropdown of live suggestions to the **native** stock parts search bar — no separate
window, no stock UI replacement.

*A Kerbalized play on Google search: "Koobal" = Kerbal + Google.*

---

## What it does

- Adds a predictive dropdown under the stock VAB/SPH search field.
- Suggests, as you type:
  - **Parts** by title, name, tags, description, module, manufacturer, tech.
  - **Stock categorizer filters** — functions, categories, manufacturers, diameters,
    tags, modules, resources, tech tiers (each with a live part count).
  - **Mods & authors** — mod name, mod suite, and part/mod author (enriched from
    `.version` files + the CKAN registry when present).
- Shows **recent searches** when the field is empty (with a clear-history button).
- Clicking a suggestion applies the matching stock filter / search and closes the
  dropdown. Category/function rows also show the stock category icon.
- Empty-field footer shows the multicolor Koobal wordmark (branding).

Suggestions are indexed **during the loading screen** (after you pick a save), so the
editor opens without a build hitch.

## Scope for this beta (important)

This build is the **core-only, stability-first baseline**. Included:
core metadata/part search, predictive dropdown, search history, branding wordmark,
and read-only stock category display icons.

**Intentionally deferred** (removed from this line to keep the stock UI 100% native):
- Subassembly search
- Custom-category search
- All Harmony patching of stock category / subassembly / delete / refresh internals

Stock parts list, filter buttons, category tabs, the subassembly tab, and delete all
behave **natively** — Koobal only reads stock data and applies the stable v0.7.x
search path.

## Install

1. Close KSP.
2. Copy `GameData/KoobalSearchEngine/` into your KSP `GameData/` (merge/overwrite).
3. Launch KSP → open the VAB or SPH.
4. `KSP.log` should contain: `[Koobal] Koobal Search Engine v0.8.5.0 active.`

### Requirements / dependencies

- **KSP 1.12.x** (built and tested against 1.12.5).
- **Harmony 2** — the `000_Harmony` folder MUST be present in your `GameData/`.
  Koobal will not load without it. Most modded installs already have it; if not,
  download HarmonyKSP and copy its `GameData/000_Harmony/` into your `GameData/`:
  https://github.com/KSPModdingLibs/HarmonyKSP/releases
- Nothing else is required — Community Category Kit, ModuleManager, etc. are **not** needed.

## Try it

Click the stock parts search bar and type, e.g.: `mk2`, `engine`, `intake`,
`rockomax`, `liquid`, `squad`, or a mod/author name. Click a row to apply it.
Clear the field to see recent searches.

## Reporting issues (beta)

Please include your `KSP.log` and the query/steps that reproduce the problem.

For verbose diagnostics, create
`GameData/KoobalSearchEngine/PluginData/DebugSettings.cfg` containing:

```
verbose = true
```

Restart the editor. Beta ships with verbose logging **off** — only a concise startup
banner plus genuine warnings/errors are logged by default.

## Changelog (recent)

### v0.8.5.0-beta — optimization + sanitary beta pass
- **Performance:** removed a redundant per-keystroke field re-scan in part matching;
  reuse cached mod part counts instead of re-scanning all parts per suggestion;
  suppressed per-keystroke diagnostic string building on the hot path.
- **Logging:** verbose dev logging now gated behind `verbose = true` (off by default);
  concise startup/version banner retained; warnings/errors always logged.
- **History:** ships with no dev history — a clean `History.cfg` is created on first
  use; legacy Koogle/PartSearchSuggest migration still works.
- **Package hygiene:** ships only DLL, `.version`, README, and a clean default
  `PluginData/BrandingSettings.cfg`. The branding wordmark is drawn programmatically,
  so the branding PNGs are unused in-game and now live in the source `BrandingAssets/`
  store rather than the shipped package.
- No behavior/feature changes vs v0.8.4.0.

### v0.8.4.0 — core-only re-baseline
- Stripped back to the stable v0.7.x core; removed subassembly + custom-category
  search and all invasive stock-UI Harmony patching. Kept metadata/part search,
  history, branding, and read-only category icons. Stock UI is 100% native.

## Branding

The empty-search footer renders a programmatic TextMeshPro wordmark (Google-Catull
parody, transparent background). Switch variants in
`PluginData/BrandingSettings.cfg`:

```
dropdownBranding = Wordmark      # default
# dropdownBranding = FullTagline # adds "SEARCH ENGINE" + tagline
```

Restart the editor after editing (config loads once per game session).

## License

MIT — see the bundled `LICENSE` file in this folder for the full text.

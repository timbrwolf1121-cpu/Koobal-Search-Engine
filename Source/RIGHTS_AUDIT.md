# Koobal Search Engine — Rights & Permissions Audit (pre-v0.9.0)

**Audit date:** 2026-07-06  
**Auditor scope:** Legal / redistribution safety for limited v0.9.0 rollout (no plugin code changes)  
**Mod version on disk:** 0.6.9  
**Overall verdict:** **YELLOW** — no RED blockers for a single trusted-tester zip; resolve P0 items before any external share

---

## Executive summary

| Area | Verdict | Notes |
|------|---------|-------|
| Source code ownership | **GREEN** | 18 production `.cs` files; no copied mod/Stack Overflow/decompiled paste detected |
| Dependencies & redistribution | **GREEN** | Build-time refs only (`Private=false`); does not ship KSP/Unity/Harmony DLLs |
| GameData assets | **GREEN** | No bundled textures/fonts/audio; runtime uses Unity `whiteTexture` + stock TMP |
| Runtime data sources | **GREEN** | CKAN registry, `.version` files, part data — read-only from user's install |
| Release package hygiene | **YELLOW** | `.pdb` produced on Release build; `History.cfg` is user-local; README misstates Harmony |
| License declaration | **YELLOW** | MIT in `.version` only; no `LICENSE` file in GameData |
| KSP / Squad policy (v1.0) | **YELLOW** | Reflection + Harmony on stock editor is standard; public source required for Squad-hosted release |

**RED blockers:** None identified for limited personal rollout.

---

## 1. Source code ownership

### Production files reviewed (18)

| File | Lines (approx.) | Role | Original? |
|------|-----------------|------|-----------|
| `Plugin.cs` | 31 | KSPAddon bootstrap, Harmony init | Yes — minimal glue |
| `EditorSearchHook.cs` | 1,058 | Hooks `PartCategorizer.searchField`, orchestrates UI/index | Yes |
| `StockSearchGuard.cs` | 119 | Harmony patches (3 targets) | Yes — standard HarmonyLib patterns |
| `StockSearchHelper.cs` | 1,052 | Reflection invoke of stock search/filter APIs | Yes — original; uses discovered member names |
| `SearchDropdownPanel.cs` | 1,149 | Programmatic Unity UI dropdown | Yes |
| `PartsPanelCollapseHelper.cs` | 538 | Editor panel slide/hide via stock transitions + reflection | Yes |
| `SuggestionIndex.cs` | 491 | Part metadata search index | Yes |
| `MetadataSuggestionIndex.cs` | 597 | Author/mod suggestion index | Yes |
| `CategorizerSuggestionIndex.cs` | 680 | Stock categorizer filter suggestions | Yes |
| `StockCategorizerHelper.cs` | 245 | Predicate resolution via stock filter fields | Yes |
| `ModMetadataCache.cs` | 471 | Reads installed mod `.version` files | Yes |
| `CkanAuthorRegistry.cs` | 240 | Regex parser for local `CKAN/registry.json` | Yes — custom, not CKAN library |
| `AuthorAttribution.cs` | 105 | Author filter matching | Yes |
| `AuthorTokenizer.cs` | 97 | Author string tokenization | Yes |
| `AuthorCanonicalizer.cs` | 218 | Author alias grouping | Yes |
| `AuthorMatchHelper.cs` | 175 | Author query scoring | Yes |
| `PartSuggestion.cs` | 92 | Suggestion DTO / enums | Yes |
| `SearchHistory.cs` | 82 | Local `PluginData/History.cfg` persistence | Yes |

### Dev-only (excluded from player zip)

| Path | Purpose |
|------|---------|
| `tools/ReflectSearch/Program.cs` | One-off reflection probe for `PartCategorizer` API discovery |
| `tools/ReflectSearch.cs` | Marked "Temporary reflection probe — delete after use" |

### Findings

- **No copyright/license headers** in source — acceptable for original MIT authorship; add SPDX/header at v1.0 if desired.
- **No attribution comments** to other mods, Stack Overflow, or decompiler output.
- **Harmony patches** (`StockSearchGuard.cs`): idiomatic `[HarmonyPatch]` + `Prefix` returning `false` to skip original — community-standard HarmonyLib usage, not copied from a specific mod.
- **Reflection** (`StockSearchHelper`, `PartsPanelCollapseHelper`): invokes private stock methods/fields by name. This is ubiquitous in KSP modding. Squad's [add-on posting rules](https://www.kerbalspaceprogram.com/add-on-posting-rules) state you may use public/protected members and must not decompile or redistribute game DLLs; reflection on private editor internals is a known grey area but not a redistribution violation. No decompiled code is embedded in the mod sources.
- **CKAN JSON parsing** (`CkanAuthorRegistry.cs`): hand-written regex/brace parser — not a copy of CKAN client code.

---

## 2. Dependencies & redistribution

### `PartSearchSuggest.csproj` reference table

| Reference | Hint path | `Private` | Shipped in mod zip? |
|-----------|-----------|-----------|---------------------|
| `Assembly-CSharp` | `KSP_x64_Data/Managed/` | **false** | **No** |
| `0Harmony` | `GameData/000_Harmony/0Harmony.dll` | **false** | **No** |
| `UnityEngine` (+ modules) | `KSP_x64_Data/Managed/` | **false** | **No** |
| BCL / .NET Framework 4.7.2 | Framework | n/a | Compiled into `KoobalSearchEngine.dll` only |

All game and third-party DLLs are **build-time references only**. Release output is a single plugin assembly.

### Harmony2 (`000_Harmony`)

| Question | Answer |
|----------|--------|
| Do we redistribute Harmony? | **No** — runtime dependency; user installs `000_Harmony` separately |
| Harmony license | **MIT** ([HarmonyKSP ReadMe](https://github.com/KSPModdingLibs/HarmonyKSP), [LICENSE](https://github.com/KSPModdingLibs/HarmonyKSP/blob/main/LICENSE)) |
| Is `depends: Harmony2` in CKAN sufficient? | **Yes** — per HarmonyKSP guidance; CKAN installs `Harmony2` before this mod |
| Should we bundle Harmony in zip? | **No** — causes version conflicts; document dependency in README |
| `KSPAssemblyDependency` attribute | **Not present** — optional best practice for v1.0; not a legal blocker |

---

## 3. Assets in GameData

### Current `GameData/KoobalSearchEngine/` inventory

| Path | Type | Shipped in v0.9.0 zip? | Rights notes |
|------|------|------------------------|--------------|
| `KoobalSearchEngine.version` | Metadata | **Yes** | Declares `license = MIT` |
| `README.txt` | Docs | **Yes** | Original text; contains inaccurate Harmony claim (see §6) |
| `Plugins/KoobalSearchEngine.dll` | Plugin binary | **Yes** | Original compiled output |
| `Plugins/KoobalSearchEngine.pdb` | Debug symbols | **No** — exclude | Build artifact; not for distribution |
| `PluginData/History.cfg` | User data | **No** — exclude | User search history (privacy) |

**No** textures, sprites, fonts, audio, or MM patches in GameData.

### Runtime asset usage (not redistributed)

| Asset | Usage | Acceptable? |
|-------|-------|-------------|
| `Texture2D.whiteTexture` | `SearchDropdownPanel.GetWhiteSprite()` for opaque UI Images | **Yes** — Unity built-in; standard Unity UI pattern |
| `TextMeshProUGUI` | Dropdown labels created at runtime | **Yes** — uses KSP/Unity TMP font from game context; no font files bundled |
| Stock editor UI hierarchy | Parents overlay under existing editor Canvas | **Yes** — no Squad texture files copied into mod folder |

---

## 4. Runtime vs redistributed data

| Data source | Access | Redistributed? | Legal notes |
|-------------|--------|----------------|-------------|
| `CKAN/registry.json` | Read at runtime (`CkanAuthorRegistry`) for author enrichment | **No** | User-local CKAN install data; not copied into mod package. Author names shown in UI are factual metadata from the user's machine. |
| Other mods' `*.version` | Read at runtime (`ModMetadataCache`) | **No** | Reads files already on user's disk; displays mod name/author for search — standard attribution. |
| `PartLoader.Instance.loadedParts` | KSP public mod API | **No** | Stock + installed mod parts loaded in-game. |
| `AvailablePart` fields | KSP API | **No** | Title, tags, manufacturer, etc. indexed for search. |
| `PluginData/History.cfg` | Written at runtime | **No** (must not ship) | User-generated search history. |

---

## 5. Third-party content in distributed zip

### v0.9.0 intended package (from `ROLLOUT_PLAN.md`)

```
PartSearchSuggest-0.9.0.zip
└── GameData/
    └── PartSearchSuggest/
        ├── PartSearchSuggest.version
        ├── README.txt
        ├── CHANGELOG.md          # planned
        ├── LICENSE               # planned — P0
        └── Plugins/
            └── PartSearchSuggest.dll
```

### Must exclude

| Item | Reason |
|------|--------|
| `*.pdb` | Debug symbols (Release build writes `PartSearchSuggest.pdb` alongside DLL) |
| `PluginData/History.cfg` | User-specific / privacy |
| `000_Harmony/` | Separate dependency |
| `Source/`, `tools/`, `obj/` | Dev artifacts |
| `ROLLOUT_PLAN.md`, `RIGHTS_AUDIT.md` | Internal planning (optional: ship audit is not required) |
| Any other mod's files | Accidental inclusion check — **none found** in PartSearchSuggest tree |

### Accidental inclusion check

- No `Assembly-CSharp.dll`, `UnityEngine*.dll`, or `0Harmony.dll` under `GameData/PartSearchSuggest/`.
- No assets from ReStock, MM, or other mods in this folder.

---

## 6. License declaration & documentation accuracy

| Item | Status | Priority |
|------|--------|----------|
| MIT in `PartSearchSuggest.version` | Present (`license = MIT`) | — |
| `GameData/PartSearchSuggest/LICENSE` (full MIT text) | **Missing** | **P0** before external share |
| README Harmony claim | **Incorrect** — README § "Known POC limits" says "No Harmony patches" but `StockSearchGuard.cs` applies 3 Harmony patches | **P0** — fix before share (accuracy + dependency disclosure) |
| README Harmony dependency | **Missing** in install section | **P0** |
| Public source (Squad plugin rule) | Not required for private v0.9.0 tester zip; **required for v1.0** public GitHub/SpaceDock/forum | **P1** (v1.0) |
| `CHANGELOG.md` | Missing | P1 (v0.9.0 package quality) |

### KSP / Squad policy (Harmony + reflection)

- **Modding is generally permitted**; Harmony patching of stock `PartCategorizer` / `BasePartCategorizer` is standard practice across the ecosystem.
- **EULA nuance:** Do not redistribute or decompile KSP/Unity DLLs. This mod complies.
- **Squad add-on rule §5 (source):** Plugins offered on Squad-maintained community services must have **publicly available source**. Applies to v1.0 public release, not necessarily a one-person trusted zip if source is shared privately with the tester.
- **Squad add-on rule §9:** Use public/protected API where possible; reflection on private editor methods is common but technically stricter than the letter of the rule — not a redistribution issue.

---

## 7. Dependency license table

| Dependency | How used | License | Redistributed? | Compliance action |
|------------|----------|---------|----------------|-------------------|
| **Harmony2** (0Harmony) | Compile ref + runtime load from `000_Harmony` | MIT | **No** | Declare in README + CKAN `depends: Harmony2` |
| **KSP / Assembly-CSharp** | Compile ref + runtime from game | Proprietary (owned game) | **No** | User must own KSP |
| **Unity Engine modules** | Compile ref + runtime from game | Proprietary (Unity) | **No** | Bundled with KSP |
| **TextMeshPro** (TMPro) | Compile ref via KSP | Unity / included in KSP | **No** | Runtime font from game |
| **.NET Framework 4.7.2 BCL** | Compile target | Microsoft (.NET Framework license) | Compiled into plugin | Standard for KSP plugins |
| **CKAN registry data** | Runtime read | CKAN metadata (per-mod licenses apply to mods, not this read) | **No** | Read-only enrichment |
| **Other mods' `.version` metadata** | Runtime read | Per-mod | **No** | Factual attribution in UI |

---

## 8. Action items by priority

### P0 — before any external share (v0.9.0 gate)

1. **Add `GameData/PartSearchSuggest/LICENSE`** — full MIT text matching `.version` declaration.
2. **Update `README.txt`** — document **Requires Harmony2 (HarmonyKSP)**; remove "No Harmony patches" claim; add install link to [HarmonyKSP releases](https://github.com/KSPModdingLibs/HarmonyKSP/releases).
3. **Release zip checklist** — exclude `*.pdb`, `PluginData/History.cfg`, `tools/`, `obj/`, `Source/`.
4. **Verify package contents** — only DLL + `.version` + README + LICENSE (+ CHANGELOG when added).

### P1 — before v1.0 public release

5. **Publish source** on GitHub (Squad plugin rule).
6. **Add `CHANGELOG.md`** to release zip.
7. **CKAN NetKAN** with `"depends": [{ "name": "Harmony2" }]`, `"license": "MIT"`.
8. **Optional:** `[assembly: KSPAssemblyDependency("0Harmony", 2, 2, 1)]` for loader clarity.
9. **Replace `url = local`** in `.version` with repository URL.

### P2 — hygiene / best practice

10. Add copyright/SPDX header to source files (optional).
11. Delete or gitignore `tools/ReflectSearch/` when no longer needed.
12. Document reflection/Harmony approach in README for transparency (not a license requirement).

---

## 9. File-by-file ship inventory (v0.9.0 zip)

| Include | SHA purpose | Rights status |
|---------|-------------|---------------|
| `Plugins/PartSearchSuggest.dll` | Plugin | Original work — MIT |
| `PartSearchSuggest.version` | AVC / compatibility | Original metadata |
| `README.txt` | User docs | Original (fix Harmony section) |
| `LICENSE` | Legal grant | **To add** — MIT |
| `CHANGELOG.md` | Release notes | **To add** — original |

| Exclude | Why |
|---------|-----|
| `PartSearchSuggest.pdb` | Dev artifact |
| `PluginData/History.cfg` | User data |
| Everything else under `GameData/PartSearchSuggest/` | N/A |

---

*Planning document only. No plugin code was modified during this audit.*

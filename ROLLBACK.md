# Koobal Search Engine — Rollback Guide

*(Formerly Part Search Suggest / PartSearchSuggest.)*

**Last updated:** 2026-07-11

---

## ✅ CURRENT VERIFIED SAFE BASELINE — v0.8.4.0

**User-confirmed working in-game on 2026-07-08** (ModTest install). This is the
designated safe/rollback version. It is now preserved by **three independent, restorable
artifacts** — no longer prose-only:

| # | Artifact | Location | Restores by |
|---|----------|----------|-------------|
| 1 | **Git annotated tag `v0.8.4.0`** | repo at `Source/PartSearchSuggest/.git` (tag msg: "Verified safe core-only re-baseline") | `git checkout v0.8.4.0` + rebuild |
| 2 | **In-repo ReleaseArchive zip** | `Source/PartSearchSuggest/ReleaseArchive/KoobalSearchEngine_v0.8.4.0_CORE_STABLE.zip` | unzip into KSP `GameData/` |
| 3 | **Desktop stable zip** | `C:\Users\timbr\Desktop\KoobalSearchEngine_v0.8.4.0_CORE_STABLE.zip` | unzip into KSP `GameData/` |

Both zips are byte-identical (447,245 bytes) CKAN-style payloads containing
`GameData/KoobalSearchEngine/`: the `0.8.4.0` DLL, `KoobalSearchEngine.version`, `README.txt`,
`PluginData/BrandingSettings.cfg`, and `Textures/*.png`.

**Feature scope (core-only):** metadata search (parts, categorizer filters/categories,
manufacturers, tags, authors, mod names/suites) + predictive dropdown + history/clear +
branding wordmark/tagline + `BrandingSettings` toggle + **read-only category display icons**.
**Deliberately NOT included:** subassembly search, custom-category search, and ALL stock-UI /
category / subassembly / refresh / delete / displayType Harmony patching. Stock parts list,
filter buttons, category tabs, subassembly tab, and delete are 100% native.

### How to restore v0.8.4.0

**Option A — from a zip (fastest, no build):**
1. Close KSP.
2. Unzip either zip (artifact #2 or #3) so its `GameData\KoobalSearchEngine\` merges into
   your target install's `GameData\` (e.g.
   `F:\SteamLibrary\steamapps\common\Kerbal Space Program\GameData\`), overwriting.
3. Launch KSP → VAB and confirm `[Koobal]` bootstrap in `KSP.log`.

**Option B — from git (rebuild from source):**
```powershell
cd "F:\SteamLibrary\steamapps\common\Kerbal Space Program\Source\PartSearchSuggest"
git checkout v0.8.4.0
# csproj OutputPath writes to the MAIN install by default. To build without touching an
# install, redirect: dotnet build -c Release -o "$env:TEMP\kse_v0840"
dotnet build PartSearchSuggest.csproj -c Release
```
Verify the built DLL reports `0.8.4.0`:
```powershell
[System.Reflection.AssemblyName]::GetAssemblyName("<path>\KoobalSearchEngine.dll").Version
```

> **Going-forward policy:** every future release MUST create both (a) a commit + annotated
> git tag and (b) a built zip in `ReleaseArchive/`. See `RELEASE_PROCESS.md`. No version may
> ever be preserved as prose only again.

---

## Version lineage

| Label | csproj / assembly | KSP `.version` | Status | Notes |
|-------|-------------------|----------------|--------|-------|
| **v0.8.5.2a** | `0.8.5.3` / `0.8.5.3` (display `0.8.5.2a`) | `0.8.5` (+BUILD 3) | **Current — dual-deployed to main + ModTest.** Rollback baseline remains v0.8.4.0. | **SpaceDock / CKAN Express packaging.** Same gameplay as v0.8.5.2; short player README; internal `KoobalSearchEngine.ckan` → `depends: Harmony2`; sanitary zip `KoobalSearchEngine_v0.8.5.2a.zip`. Assembly bumped to `0.8.5.3` because letter builds cannot live in AssemblyVersion / AVC integers. Annotated tag **`v0.8.5.2a`**. Author `timbrwolf1121`. |
| **v0.8.5.2** | `0.8.5.2` / `0.8.5.2` | `0.8.5` (+BUILD 2) | **Superseded by v0.8.5.2a.** Rollback baseline remains v0.8.4.0. | **Branding footer centering** plus **organic apply race restore (same assembly, no version bump).** (1) Empty-query wordmark stays centered with history/resize. (2) `StockSearchGuard` again blocks void `SearchStart` while a Koobal custom filter is active (lost v0.7 / over-cleared in v0.8.5.1) so `ApplyPrecisePart` / categorizer apply survive blur instead of loose stock overwrite; typing still clears via `CancelPendingStockSearchForTyping`; never skip `SearchRoutine`. Annotated tag **`v0.8.5.2`**. Author `timbrwolf1121`. |
| **v0.8.5.1-beta** | `0.8.5.1` / `0.8.5.1` | `0.8.5` (+BUILD 1) | **Superseded by v0.8.5.2.** Rollback baseline remains v0.8.4.0. | Same assembly line as v0.8.5.1, packaged as forum beta zip `KoobalSearchEngine_v0.8.5.1-beta.zip`. **Suggestion quality rebalance:** categorizer/metadata above parts (`RankScore` 100+); flat top-8 categorizer by RankScore (no harsh 3-row token bucket); denylist synonym junk; no `resourceInfo` prose scraping; query-length-aware part scoring (≤2 title-first / ≥3 tag-weighted). Annotated tag **`v0.8.5.1-beta`**. Author `timbrwolf1121`. |
| **v0.8.5.1** | `0.8.5.1` / `0.8.5.1` | `0.8.5` | **Code lineage / intermediate tag (superseded as “current deploy” by v0.8.5.2).** | **FIX + QoL under one label.** (1) **SearchStart NRE:** do not skip `SearchRoutine`; block void `SearchStart` while apply-suppressed; clear custom-filter on focus/typing (`RecoverAfterFailedApply`). **Over-cleared on SearchStart after apply** — restored under v0.8.5.2. (2) Hangar-free index load. (3) 1080p readability. (4) Branding centering partial → fixed in v0.8.5.2. (5) Clicked-suggestion history. (6) Clear-history trashcan. (7) Suggestion quality rebalance. |
| **v0.8.5.0-beta** | `0.8.5.0` / `0.8.5.0` | `0.8.5` | **Superseded on ModTest by v0.8.5.1. Release artifacts preserved (do not overwrite).** | **OPTIMIZATION + SANITARY BETA PASS (no behavior change vs v0.8.4.0).** Internal cleanup/optimization only — same feature set as the v0.8.4.0 core-only baseline. **Optimized hot paths:** (1) `SuggestionIndex.Match` / `GetEnterQueryMatches` dropped the redundant `WordsMatch` pre-filter (removed dead `WordsMatch`/`WordMatchesAnyField`/`FieldMatches`) — `ScorePart` already returns `Score < 0` on any non-matching word, so every query now scans each part's fields once instead of twice; (2) `MetadataSuggestionIndex.CreateModNameSuggestion` reuses the cached `mod.PartCount` (computed once in `FinalizeMetadataIndex`) instead of calling `ModFilterMatcher.CountPartsMatchingModFolder` (a full loaded-parts scan) per mod suggestion per keystroke; (3) per-keystroke diagnostic string builders (`LogAuthorMatches`, `LogIntakeQueryMatches`, `LogDedup`) now early-out when verbose logging is off. Frame-sliced/deferred index builds and the `EditorPartAvailability` warm cache are unchanged. **Logging:** new `DebugSettings.verbose` flag (default **false**) gates `EditorBootstrap.Log`; added `EditorBootstrap.LogAlways` used for a one-time concise startup banner (`Koobal Search Engine vX.Y.Z.B active.`); warnings/errors always logged. **History:** ships with NO `History.cfg` (clean first-run file created on first search); Koogle/PartSearchSuggest → Koobal migration paths intact. **Package:** ships DLL, `.version`, README (beta-appropriate, concise changelog), `Textures/*.png`, `PluginData/BrandingSettings.cfg` (clean default); the dev-only `rollback/README.txt` is no longer shipped. Build clean (0 warnings / 0 errors). |
| **v0.8.4.0** | `0.8.4.0` / `0.8.4.0` | `0.8.4` | **VERIFIED SAFE ROLLBACK BASELINE (was ModTest deploy; superseded on ModTest by v0.8.5.0-beta). Main left at v0.8.3.17 by user request.** | **CORE-ONLY RE-BASELINE — de-risk.** Strips the mod back to the stable v0.7.x core and removes every deep stock-integration feature that repeatedly broke stock UI. **Removed:** subassembly search (`SubassemblyMatcher`, `SubassemblyEntry`, `SubassemblySuggestionIndex`, `StockSubassemblyHelper`, `SubassemblyLifecycleHooks`, `AllSubassemblies`); custom-category search (`CustomCategoryMatcher`, `CustomCategoryEntry`, `CustomCategorySuggestionIndex`, `StockCustomCategoryHelper`, `CustomCategoryLifecycleHooks`, `CustomCategoryCfgReader`); `StockTabNavigator`; `PartCategorizerUiHelper` (tab activation + displayType snapshot/restore); trimmed `SuggestionKind` to core kinds; and **all Harmony patches touching stock category/subassembly/refresh/delete/lifecycle/displayType internals**. `StockSearchGuard` reduced to the minimal v0.6.7-era race guard (block stock `SearchRoutine`/`SearchFilterResult` overwrite only while a Koobal apply/custom-filter is active); removed typing-pipeline suppression, `SearchStop` manipulation, and `EditorPartList.Refresh` blocking. **Kept:** predictive dropdown, metadata suggestions (parts, categorizer filters/categories, manufacturers, tags, authors, mod names/suites), history + clear, 0-part/single-letter suppression, dedup, branding wordmark + `BrandingSettings` toggle + Textures, and read-only category display icons from live `PartCategorizer` buttons (icon eligibility: real category rows only). **HARD RULE:** stock parts list, filter buttons, category tabs, subassembly tab, and delete are now 100% native; the only stock interaction is passive reads + the stable v0.7.x apply path (search field + `Refresh(PartSearch)` / `SearchFilterResult`). Build clean (0 warnings / 0 errors). Stable baseline zip: `Desktop/KoobalSearchEngine_v0.8.4.0_CORE_STABLE.zip`. |
| **v0.8.3.17** | `0.8.3.17` / `0.8.3.17` | `0.8.3` | **Superseded on ModTest (still on main install)** | **CRITICAL fix — stock category buttons restored.** v0.8.3.16 wrote `Category.displayType` on the **stock click path** (an `OnTrueCATEGORY` prefix forcing `EnsureCategoryChainDisplayType`, plus the `OnTrueSUB` snapshot/restore). Stock `OnTrueSUB`/`OnFalseSUB`/`OnFalseFilterOrCategory`/`RemoveSubcategoryButtons` branch on `displayType`; the parts list blanks via `filterGenericNothing` when it desyncs — so every stock category button (Structural/RCS/Control/…) silently stopped filtering (no exception). **Fix:** the forced-`displayType` `OnTrueCATEGORY` patch is **removed**; remaining `OnTrueCATEGORY`/`OnTrueSUB` prefixes are `void`, fail-open (try/catch), pure no-ops for stock categories, and **never write `displayType`**. `displayType` is written only transiently inside `StockTabNavigator.BeginTabApply` and reverted by `RestoreAllCapturedDisplayTypes()` on scope dispose. `ActivateSimplePartsMode` no longer writes `displayType`. **Preserved:** v0.8.3.16 delete-popup guard, custom subassembly tab scoping (fail-open `OnTrueSUB`), navigate-only apply, branding, icons. Emergency stock-safe fallback (OnTrue* patches disabled) exported to Desktop as `KoobalSearchEngine_v0.8.3.17-fallback_STABLE_FALLBACK.zip`. |
| **v0.8.3.16** | `0.8.3.16` / `0.8.3.16` | `0.8.3` | **Broken — stock category click regression** (superseded by v0.8.3.17) | **CRITICAL fixes.** **Delete:** guard blocks `EditorPartList.RefreshSubassemblies` while the stock subassembly confirm dialog is open (set in `MouseInput_Delete` postfix, cleared in `OnDismiss` prefix) — stops the mid-dialog rebuild that destroyed the item and made `OnDismiss.StartCoroutine` throw `NullReferenceException` (popup unclosable, UI locked). Index update stays in `DeleteSubassembly` postfix; no prefix delete/refresh. **Tabs:** `OnTrueSUB` prefix no longer overwrites `Category.displayType`; it snapshots the stock-original value in `EnsureCategoryChainDisplayType` and restores it (`RestoreOriginalDisplayTypeChain`) so stock scopes the list. Confirmed enum: `PartsList=0, SubassemblyList=1, CustomPartList=2, PartSearch=3` — stock `OnTrueSUB` needs `displayType==SubassemblyList(1)` (NOT PartSearch; the v0.8.3.13–15 notes were mislabeled). Navigate-only apply preserved. |
| **v0.7.0** | `0.7.0` / `0.7.0.0` | `0.7.0` | Rollback baseline | Last known good for engines, author/mod filters, and categorizer suggestions. **Intake click-to-filter broken** (`intake` → Aerodynamics bucket; blank list if labeled "Intakes"). |
| **v0.7.1a** | `0.7.1.1` / `0.7.1.1` | `0.7.1` | Superseded | First intake fix attempt. Adds `filterIntake` custom predicate, `SuggestionFilterRegistry`, SearchRoutine guard. |
| **v0.7.1b** | `0.7.1.2` / `0.7.1.2` | `0.7.1` | Not shipped | Second intake fix attempt — **use only** if you roll back v0.7.1a and re-attempt the fix. |
| **v0.7.8.0a** | `0.7.8.1` / `0.7.8.1` | `0.7.8` | Superseded | Mod testing phase baseline: index stats dump (`DebugSettings.dumpIndexStats`), `testing/` profile framework. |
| **v0.7.8.1l** | `0.7.8.13` / `0.7.8.13` | `0.7.8` | Superseded | VAB entry freeze fix (profile-02): editor part availability cache, frame-sliced index build, categorizer count trust-first-pass; ModMetadataCache dedup. |
| **v0.8.2.1** | `0.8.2.1` / `0.8.2.1` | `0.8.2` | **Current deploy** | **Icon fix:** RUI Icon uses `UnityEngine.Texture2D` directly (not `.main` wrapper). **Apply fix:** custom category clicks navigate stock tabs only — no transient parts-list filter. Subassembly craft apply unchanged (tab + single-craft filter). |
| **v0.8.2.0** | `0.8.2.0` / `0.8.2.0` | `0.8.2` | Superseded | **Custom category icons:** cfg `icon =` parsed per subcategory; stock `IconLoader.GetIcon` → cached Sprite; ~20px icon on custom category dropdown rows only; `parent › subcategory` for colliding part subcategory names. No part/subassembly thumbnails. |
| **v0.8.1.8** | `0.8.1.8` / `0.8.1.8` | `0.8.1` | Superseded | **Custom subcategory tab navigation:** parent tab activates before subcategory on part custom category apply; one-frame deferred stock merge after `LoadCustom*`; enrich resolve warnings suppressed; index log shows `part:Parent:Sub` keys. No new filtering logic. |
| **v0.8.1.7** | `0.8.1.7` / `0.8.1.7` | `0.8.1` | Superseded | Custom category dropdown fix — cfg-bootstrapped categories visible; zero-count rows stay visible; live merge updates counts when PartCategorizer ready. |
| **v0.8.1.6** | `0.8.1.6` / `0.8.1.6` | `0.8.1` | Superseded | **Custom category dropdown fix:** cfg-bootstrapped categories no longer gated on live `TryResolveEntry` in `Match()`/`IsValid()`; zero-count rows stay visible (`0 parts` / `0 subassemblies`); live merge still updates counts when PartCategorizer ready. |
| **v0.8.1.5** | `0.8.1.5` / `0.8.1.5` | `0.8.1` | Superseded | **Custom category cfg bootstrap:** install-level `PartCategories.cfg` + `SubassemblyCategories.cfg` parsed once on editor entry; fixed `displayCategoryName` reflection; removed 15s poll; instant stock merge for live counts/apply. |
| **v0.8.1.4** | `0.8.1.4` / `0.8.1.4` | `0.8.1` | Superseded | **P0 CTD fix:** `SearchStop` Harmony prefix no longer calls `InvokeBaseSearchStop` (virtual dispatch re-entered prefix → `StackOverflowException` on VAB entry). `CancelPendingStockSearchForTyping` uses flag reset only. Custom category poll moved to background so dropdown initializes immediately. |
| **v0.8.1.3** | `0.8.1.3` / `0.8.1.3` | `0.8.1` | Broken — CTD | **P0 typing leak fix:** `CancelPendingStockSearchForTyping` no longer triggers `PartCategorizer.SearchStop` refresh side-effect; blocks `EditorPartList.Refresh` + `PartCategorizer.SearchRoutine` during typing guard; Enter no-match stock fallback removed; first-focus + custom category scan fixes. **Regression:** `SearchStop` prefix called `InvokeBaseSearchStop` → infinite recursion / stack overflow on editor entry. |
| **v0.8.1.2** | `0.8.1.2` / `0.8.1.2` | `0.8.1` | Superseded | **Core value prop:** block stock `SearchField_OnValueChange` / `SearchStart` / `SearchRoutine` / `SearchFilter_` during typing — parts list changes only on suggestion click (or explicit `ApplySearch` for history rows). Cancels orphaned `SearchRoutine` on each keystroke; `ApplySearch` uses `EnterAllowStockTextSearch` + `SetTextWithoutNotify`. |
| **v0.8.1.1** | `0.8.1.1` / `0.8.1.1` | `0.8.1` | Superseded | Custom category search fixes — delayed initial scan until `LoadCustom*` completes; fixed `UIRadioButton.SetState` signature for tab activation; subassembly apply switches to owning custom category tab + single-craft filter; part suggestions force standard parts list via `SetSimpleMode`. |
| **v0.8.1.0** | `0.8.1.0` / `0.8.1.0` | `0.8.1` | Superseded | Custom category search — incremental `CustomCategorySuggestionIndex` hooks for create/add/move/delete; apply via advanced/subassembly tab + transient filter (mirrors subassembly v0.8.0.3); delete postfix after stock confirms. |
| **v0.8.0.4** | `0.8.0.4` / `0.8.0.4` | `0.8.0` | Superseded | Subassembly delete fix — transient `KoobalSearchEngine_Subassembly` filter cleared on delete dialog, tab change, search focus, dropdown dismiss, and 45s timeout; index remove runs after stock delete (postfix); resets stock search flags before delete refresh. |
| **v0.8.0.3** | `0.8.0.3` / `0.8.0.3` | `0.8.0` | Superseded | Subassembly apply fix — switch to stock subassembly tab and filter `saList` to indexed craft path (no `TapIcon` spawn); clears part search first to stop medkit/strut mis-apply. |
| **v0.8.0.2** | `0.8.0.2` / `0.8.0.2` | `0.8.0` | Superseded | Empty-query browse fallback — when search history is empty, clicking the search bar shows top categorizer filter suggestions instead of hiding the dropdown. |
| **v0.8.0.1** | `0.8.0.1` / `0.8.0.1` | `0.8.0` | Superseded | Rebrand to **Koobal Search Engine** — Kerbalized play on Google search (`Koobal` = Kerbal + Google); GameData `KoobalSearchEngine/`, DLL `KoobalSearchEngine.dll`, log prefix `[Koobal]`; config migration from `KoogleSearchEngine/PluginData/` and legacy `PartSearchSuggest/PluginData/`. |
| **v0.8.0.0** | `0.8.0.0` / `0.8.0.0` | `0.8.0` | Superseded | Subassembly search — editor folder scan, incremental save/delete hooks, `TapIcon` apply. Shipped as **Koogle Search Engine** (`KoogleSearchEngine/`). |
| **v0.7.9.1** | `0.7.9.1` / `0.7.9.1` | `0.7.9` | Superseded | Save-load indexing via `GameLoadBootstrap` (`EveryScene`); main menu no-op; VAB UI hook only. |
| **v0.7.9.0** | `0.7.9.0` / `0.7.9.0` | `0.7.9` | Superseded | Deferred metadata/categorizer indexing; Search ready (basic/full) logs; loading placeholder/header; profile-main-full stress prep. |
| **v0.7.8.1k** | `0.7.8.12` / `0.7.8.12` | `0.7.8` | Superseded | Suggestion row layout fix — separate title/subtitle TMP elements, fixed row heights, RectMask2D + ellipsis; prevents text overlap/bleed on long metadata queries. |
| **v0.7.8.1j** | `0.7.8.11` / `0.7.8.11` | `0.7.8` | Superseded | Rebrand to **Koogle Search Engine** — GameData `KoogleSearchEngine/`, DLL `KoogleSearchEngine.dll`, log prefix `[Koogle]`; config migration from legacy `PartSearchSuggest/PluginData/`. Superseded by v0.8.0.1 Koobal rebrand. |
| **v0.7.8.1i** | `0.7.8.10` / `0.7.8.10` | `0.7.8` | Superseded | Aggressive recovery: removed hold/preempt/focus/mask/watchdog; Show→Collapse / Hide→Restore only; lightweight `Transition("In")` guard while dropdown open. |
| **v0.7.8.1h** | `0.7.8.9` / `0.7.8.9` | `0.7.8` | Broken | Fix 1g click regression attempt: focus alone no longer holds collapse; empty show calls AbortShowAttempt + restore; session hold only after successful show or typing; 45-frame watchdog. |
| **v0.7.8.1g** | `0.7.8.8` / `0.7.8.8` | `0.7.8` | Broken | Empty history click: `_searchFieldFocused` kept hold after EndPreemptiveHold — parts list gone forever, no dropdown. |
| **v0.7.8.1f** | `0.7.8.7` / `0.7.8.7` | `0.7.8` | Broken | `BeginDropdownHold` set `_searchFieldFocused=true` — hold mask never released on empty history / spurious PointerDown. |
| **v0.7.8.1e** | `0.7.8.6` / `0.7.8.6` | `0.7.8` | Superseded | Extended hold (dropdown OR search focus), PointerDown preempt, TransitionImmediate guard, no premature restore. |
| **v0.7.8.1d** | `0.7.8.5` / `0.7.8.5` | `0.7.8` | Superseded | Instant hold mask (snap Out + alpha=0), early hold before ShowSuggestions, block all non-Out transitions, Update assert. |
| **v0.7.8.1c** | `0.7.8.4` / `0.7.8.4` | `0.7.8` | Superseded | Harmony `Transition(string)` patch disambiguation; isolated guard PatchAll; rollback moved out of GameData. |
| **v0.7.8.1b** | `0.7.8.3` / `0.7.8.3` | `0.7.8` | Broken | Clear-history visibility + tooltip, parts-panel slide-back guard — **plugin failed to load** (`AmbiguousMatchException` on `UIPanelTransition.Transition`). |
| **v0.7.8.1a** | `0.7.8.2` / `0.7.8.2` | `0.7.8` | Superseded | Search history fix (Enter/blur commit), clear-history trash button on dropdown header. |
| **v0.7.8.0b** | `0.7.8.3` / `0.7.8.3` | `0.7.8` | Skipped | Label unused — fixes shipped as **v0.7.8.1b** instead. |
| **v0.7.8.0c** | `0.7.8.4` / `0.7.8.4` | `0.7.8` | Skipped | Label unused — fix shipped as **v0.7.8.1c** instead. |

### Suffix → SemVer mapping

KSP `.version` files use `MAJOR_MINOR_PATCH` integers only (no `a`/`b` suffix). Map user-facing labels to csproj/assembly via the fourth version segment:

#### Intake-fix attempt suffixes (v0.7.1x) — historical

| User label | csproj `<Version>` | Assembly | `.version` file |
|------------|-------------------|----------|-----------------|
| v0.7.1a | `0.7.1.1` | `0.7.1.1` | `0.7.1` |
| v0.7.1b | `0.7.1.2` | `0.7.1.2` | `0.7.1` |

#### Mod testing phase suffixes (v0.7.8.0x) — active until v0.8.0.0

Use **v0.7.8.0(x)** for instance-specific conflict fixes during ModTest profile validation. Bump the **letter** (and csproj fourth segment); keep KSP `.version` at `0.7.8` until the v0.8.0.0 thumbnails track starts (then `.version` → `0.8.0`).

| User label | csproj `<Version>` | Assembly | `.version` file |
|------------|-------------------|----------|-----------------|
| v0.7.8.0a | `0.7.8.1` | `0.7.8.1` | `0.7.8` |
| v0.7.8.1a | `0.7.8.2` | `0.7.8.2` | `0.7.8` |
| v0.7.8.1b | `0.7.8.3` | `0.7.8.3` | `0.7.8` |
| v0.7.8.1c | `0.7.8.4` | `0.7.8.4` | `0.7.8` |
| v0.7.8.0c | `0.7.8.4` | `0.7.8.4` | `0.7.8` |
| v0.7.8.0d | `0.7.8.5` | `0.7.8.5` | `0.7.8` |
| v0.7.8.1d | `0.7.8.5` | `0.7.8.5` | `0.7.8` |
| v0.7.8.1e | `0.7.8.6` | `0.7.8.6` | `0.7.8` |
| v0.7.8.1f | `0.7.8.7` | `0.7.8.7` | `0.7.8` |
| v0.7.8.1g | `0.7.8.8` | `0.7.8.8` | `0.7.8` |
| v0.7.8.1h | `0.7.8.9` | `0.7.8.9` | `0.7.8` |
| v0.7.8.1i | `0.7.8.10` | `0.7.8.10` | `0.7.8` |
| v0.7.8.1l | `0.7.8.13` | `0.7.8.13` | `0.7.8` |
| v0.7.8.1k | `0.7.8.12` | `0.7.8.12` | `0.7.8` |
| v0.7.8.1j | `0.7.8.11` | `0.7.8.11` | `0.7.8` |

Continue incrementing the fourth segment for each new letter (`e` → `0.7.8.5`, etc.).

README changelog and zip filenames use the **user label** (e.g. `v0.7.8.0a`). Do **not** ship plain `v0.7.8` without a letter suffix during the mod testing phase.

### Re-attempt rule after rollback (v0.7.1x — historical)

If you restore v0.7.0 because v0.7.1a fails testing, any new intake-fix build must be versioned **v0.7.1b** (`0.7.1.2` in csproj). Bump csproj, README, zip name, and this file. Keep `.version` at `0.7.1` unless a full patch bump is warranted.

### Instance-specific fix rule (v0.7.8.0x — active)

When a ModTest profile fails due to a Koobal Search Engine conflict (not a mod bug), ship the fix as the **next letter suffix**:

1. Implement fix in `Source/PartSearchSuggest/`.
2. Version as **v0.7.8.0b** / **v0.7.8.0c** / … (`0.7.8.2`, `0.7.8.3`, … in csproj — **not** v0.7.9).
3. Keep KSP `.version` at **`0.7.8`** until v0.8.0.0 thumbnails track.
4. Build, deploy to ModTest, re-run the failed profile.
5. Update this file, `testing/profiles/README.md`, and README changelog.

---

## Preserved rollback artifacts

**Location:** `Source/PartSearchSuggest/rollback/v0.7.0/` (not under `GameData/` — KSP loads every `*.dll` under `GameData/` recursively)

| File | Purpose |
|------|---------|
| `PartSearchSuggest.dll` | v0.7.0 build (assembly `0.7.0.0`; no `filterIntake` / `SuggestionFilterRegistry`) |
| `PartSearchSuggest.version` | Version metadata (`version = 0.7.0`) |
| `README.txt` | v0.7.0 changelog snapshot |

Rebuilt from pre-v0.7.1a source on 2026-07-06. Live installs were **not** modified during preservation.

---

## Current deployment (do not auto-revert)

As of **v0.8.5.2a**, both installs run the same sanitary GameData package
(assembly `0.8.5.3`, player label `0.8.5.2a`). The verified-safe rollback baseline remains **v0.8.4.0**.

| Install | Version | Path |
|---------|---------|------|
| Main KSP | **v0.8.5.2a** (`0.8.5.3`) | `GameData/KoobalSearchEngine/Plugins/KoobalSearchEngine.dll` |
| ModTest | **v0.8.5.2a** (`0.8.5.3`) | `Kerbal Space Program - ModTest/GameData/KoobalSearchEngine/Plugins/KoobalSearchEngine.dll` |

Forum / Desktop / SpaceDock package: `C:\Users\timbr\Desktop\KSE\KoobalSearchEngine_v0.8.5.2a.zip`
(also in `Source/PartSearchSuggest/ReleaseArchive/`).

Stable baseline archive: `Desktop/KoobalSearchEngine_v0.8.4.0_CORE_STABLE.zip` (CKAN-style
`GameData/KoobalSearchEngine/`). To roll back either install to v0.8.4.0, unzip that
artifact (or the matching ReleaseArchive copy) over `GameData/KoobalSearchEngine/`.

**Git tags for this release:**
- `v0.8.5.2a` — annotated release tag (SpaceDock Express packaging).
- `v0.8.5.2` — prior (branding + organic apply race); kept.
- `v0.8.5.1-beta` / `v0.8.5.1` — prior lineage tags (kept; do not delete).

**Emergency stock-safe fallback:** `Desktop/KoobalSearchEngine_v0.8.3.17-fallback_STABLE_FALLBACK.zip`
— conservative build (assembly `0.8.3.17`) with the `OnTrueSUB`/`OnTrueCATEGORY` Harmony
patches disabled so Harmony never touches stock category/subassembly click methods. Stock
category buttons are guaranteed native; custom subassembly tab scoping is degraded
(acceptable). Use it if a future change reintroduces a stock-UI break.

Run ModTest profiles per `testing/TEST_RUN_WORKFLOW.md`. Instance-specific fixes ship as **v0.7.8.0b**, **v0.7.8.0c**, etc. Roll back to v0.7.0 only for catastrophic regressions (see below).

---

## Restore v0.7.0 — main KSP install

1. **Close KSP** if running.
2. Copy rollback files over the live mod folder:

```powershell
$rollback = "F:\SteamLibrary\steamapps\common\Kerbal Space Program\Source\PartSearchSuggest\rollback\v0.7.0"
$live     = "F:\SteamLibrary\steamapps\common\Kerbal Space Program\GameData\PartSearchSuggest"

Copy-Item "$rollback\PartSearchSuggest.dll" "$live\Plugins\PartSearchSuggest.dll" -Force
Copy-Item "$rollback\PartSearchSuggest.version" "$live\PartSearchSuggest.version" -Force
Copy-Item "$rollback\README.txt" "$live\README.txt" -Force
```

3. **Verify** assembly version:

```powershell
[System.Reflection.AssemblyName]::GetAssemblyName(
  "F:\SteamLibrary\steamapps\common\Kerbal Space Program\GameData\PartSearchSuggest\Plugins\PartSearchSuggest.dll"
).Version
# Expect: 0.7.0.0
```

4. Launch KSP → VAB. Confirm `KSP.log` shows PartSearchSuggest bootstrap. Test `engine` (works) and note `intake` limitation.

**Do not delete** `PluginData/History.cfg` — user search history is preserved across rollbacks.

---

## Restore v0.7.0 — ModTest install

Same steps, different target:

```powershell
$rollback = "F:\SteamLibrary\steamapps\common\Kerbal Space Program\Source\PartSearchSuggest\rollback\v0.7.0"
$live     = "F:\SteamLibrary\steamapps\common\Kerbal Space Program - ModTest\GameData\PartSearchSuggest"

New-Item -ItemType Directory -Force -Path "$live\Plugins" | Out-Null
Copy-Item "$rollback\PartSearchSuggest.dll" "$live\Plugins\PartSearchSuggest.dll" -Force
Copy-Item "$rollback\PartSearchSuggest.version" "$live\PartSearchSuggest.version" -Force
Copy-Item "$rollback\README.txt" "$live\README.txt" -Force
```

---

## Files to copy back (summary)

| From (rollback) | To (live) |
|-----------------|-----------|
| `Source/PartSearchSuggest/rollback/v0.7.0/PartSearchSuggest.dll` | `Plugins/PartSearchSuggest.dll` |
| `Source/PartSearchSuggest/rollback/v0.7.0/PartSearchSuggest.version` | `PartSearchSuggest.version` |
| `Source/PartSearchSuggest/rollback/v0.7.0/README.txt` | `README.txt` |

**Not replaced:** `PluginData/History.cfg` (per-user data).

---

## After rollback — next intake fix

1. Confirm v0.7.0 is running and intake bug reproduces.
2. Implement fix in `Source/PartSearchSuggest/`.
3. Version as **v0.7.1b** (`0.7.1.2` in csproj — not v0.7.1a).
4. Build, deploy to ModTest first, then main if tests pass.
5. Update this file with v0.7.1b notes when shipped.

---

## v0.7.1a intake-fix changes (reference)

Deployed by intake-fix subagent; logic unchanged in v0.7.1a rename (version/docs only):

- New `SuggestionFilterRegistry.cs` — function filter registry
- `filterIntake` custom predicate (`PartIsAirIntake`)
- `SearchRoutine` Harmony guard while custom filter active
- Module `FilterKey` prefers `moduleDisplayName`
- Enhanced `Row clicked` / `ApplyCategorizerFilter` logging
- Cursor rule: `.cursor/rules/partsearchsuggest-suggestions.mdc`

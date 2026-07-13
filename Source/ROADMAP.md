# Koobal Search Engine — Future Feature Roadmap

**Document version:** 2026-07-07  
**Mod:** Koobal Search Engine (`KoobalSearchEngine`) — *a Kerbalized play on Google search ("Koobal" = Kerbal + Google); formerly Part Search Suggest, formerly Koogle Search Engine*  
**Scope:** Post-v0.8 / post-v1.0 features — **planning only, no plugin code**  
**Workspace:** Koobal-only (personal parts / Titan Starship live on a separate branch — see [`docs/CLEANUP_PART_DEV_SPLIT.md`](docs/CLEANUP_PART_DEV_SPLIT.md)).

This document captures user-intent milestones beyond v0.8 search-term expansion and v1.0 public release. See [`ROLLOUT_PLAN.md`](ROLLOUT_PLAN.md) for near-term milestones. Thumbnail experiments are **deferred/abandoned** — see [`V0.8.0_THUMBNAILS_PLAN.md`](V0.8.0_THUMBNAILS_PLAN.md).

---

## Guiding principle — organic index / apply

All future suggestion types MUST follow the **index predicate === apply predicate** rule documented in [`.cursor/rules/koobal-search-engine-suggestions.mdc`](../../.cursor/rules/koobal-search-engine-suggestions.mdc):

> When a user clicks a suggestion, the filter MUST use the exact same metadata field and matching logic that generated/indexed that suggestion.

For CCK tabs and subassemblies, this means:

- **Index source === apply filter source** — if a row is indexed because it matches a CCK category predicate, clicking it must invoke the same predicate stock/CCK uses to populate that tab, not a divergent custom filter.
- Shared matchers, subtitle counts, dedup, and `PartSuggestion.IsValid()` must call the same code path as click-apply.
- Never index via field X and apply via field Y.

Current shared matchers (v0.7.x): `PartFilterMatcher`, `ModFilterMatcher`, `AuthorAttribution`, `EditorPartAvailability`. Future CCK/subassembly matchers belong in the same pattern — one matcher class, index + apply + count.

---

---

## Startup indexing architecture (v0.8.5.3 in-place+)

Indexes are **not** built on the main menu or on KSC/flight save-load. When the user opens **VAB/SPH**, indexes build **synchronously** as soon as `onGameSceneLoadRequested(EDITOR)` fires (loading buffer / prior scene) — **one** single-flight sync build, zero post-open index work. Interactive editor entry is UI-hook only; overlay uses a dedicated Overlay Canvas.

| Phase | When | Work |
|-------|------|------|
| **Main menu** | `GameScenes.MAINMENU` | Clear stale indexes only — **no database load** |
| **Editor requested** | `GameEvents.onGameSceneLoadRequested(EDITOR)` via `EditorLoadIndexWatcher` | `GameLoadIndexService` **sync** `Build()` (single-flight): `ModMetadataCache`, `EditorPartAvailability`, `SuggestionIndex`, `MetadataSuggestionIndex`, `CategorizerSuggestionIndex` |
| **Editor load fallback** | `GameScenes.EDITOR` via `GameLoadBootstrap` / `EditorBootstrap.Awake` | Same `EnsureBuildStarted` — no-ops if already ready / in progress |
| **Editor interactive** | `KSPAddon.Startup.EditorAny` after full ready | UI hook + **own Overlay canvas** dropdown; consume pre-built indexes. **No** post-open index / auto-show / stock CanvasScaler writes |

**Hook:** `EditorLoadIndexWatcher` (primary) + `GameLoadBootstrap` / `EditorBootstrap.Awake` fallback. `EditorSearchHook` waits for full readiness before creating the overlay.

**Per-save vs global:**

| Data | Scope | Built when |
|------|-------|------------|
| `ModMetadataCache` | Global (GameData `.version` files) | Editor loading transition |
| `EditorPartAvailability` | Per-save (`ResearchAndDevelopment.partTechAvailable`, `amountAvailable`) | Editor loading transition |
| Part / metadata / categorizer indexes | Per-save (filtered through availability) | Editor loading transition |
| Subassembly index (future) | Per-save folder | Editor entry only; incremental on save/delete |

**Log lines (VAB/SPH load):** `Building search index during VAB/SPH loading transition...` → `Search ready (basic)` → `Search ready (full)`.

**Log lines (interactive hangar):** `Search ready (full)` then `Hooked native editor search field` — no mid-session “Loading suggestions…” / layout finalize.

---

## Milestone overview

| Feature | Target window | Version band | Status |
|---------|---------------|--------------|--------|
| **Search term expansion** (CCK tabs, subassemblies, etc.) | v0.8.x | **v0.8.0** | **Active goal** — replaces thumbnail track |
| CCK tab indexing | v0.8.x or v1.x | **v0.8+ / v1.x** | Planned — research needed |
| Subassembly search | v0.8.0 | **v0.8.0** | **Implemented** — folder scan at editor entry; incremental save/delete hooks |
| Thumbnails in dropdown | — | v0.8.0 (was) | **Deferred/abandoned** — load cost outweighs benefit |

These features are **not** in scope for v0.9.0 limited rollout or v1.0.0 public release unless explicitly promoted after compatibility gates pass.

---

## v0.8 goal — expand searchable terms (not thumbnails)

**v0.8.x** focuses on indexing and suggesting **more of what the VAB already exposes**: CCK custom tabs, subassemblies, and other organizer surfaces — following the organic index/apply rule below. Thumbnails are **out of near-term scope** (deferred; likely abandoned given index/load cost on large mod lists).

---

## 1. CCK tab indexing

**Target:** Post-v0.8 or v0.8.x development; ship as **v1.x** feature after v1.0 (or v0.9.x if promoted early).

### User intent

If something can be clicked in the VAB/SPH parts-list tabs and it filters or shows parts, it must be **searchable and suggestable** in the Koobal Search Engine dropdown.

This applies to:

- **Community Category Kit (CCK)** custom tabs registered by mods (`CommunityCategoryKit/CCK.dll` — user's main-install organizer).
- **Generally:** any parts-list tab or filter UI that narrows the visible part set (stock function buttons, custom CCK categories, future organizer mods that expose tab-like filters).

The organic principle: **what the tab shows when clicked is exactly what the suggestion row must apply when chosen.**

### Phase description

| Phase | Goal |
|-------|------|
| **Research** | Discover how CCK registers categories, how tab clicks filter `EditorPartList`, and whether a stable public or reflection API exists. |
| **Matcher design** | Define `CckTabMatcher` (name TBD) with `PartMatchesCckCategory`, `CountMatchingCckCategory`, and registry of indexable tabs. |
| **Index build** | Enumerate active CCK categories at editor load (and on CCK refresh events if any); emit `SuggestionKind.CckTab` (or reuse categorizer kind with distinct apply path). |
| **Apply path** | On row click, invoke the same filter/tab-switch CCK uses — not a reimplemented tag/category guess. |
| **Compatibility** | Test on `profile-vab-ui-communitycategorykit`; verify no conflict with `StockSearchGuard` custom-filter lifecycle. |

### Technical notes

**Current state (v0.8.3):**

- Stock categorizer filters (`filterEngine`, manufacturers, tags, etc.) are indexed via `CategorizerSuggestionIndex` + `SuggestionFilterRegistry` and applied via `PartFilterMatcher.PartMatchesFilter`.
- **Stock + custom category icons (v0.8.3.0):** dropdown rows for stock categorizer filters resolve icons from live `PartCategorizerButton` / `Icon` via `StockCategorizerIconHelper` + `CustomCategoryIconHelper`. Custom cfg categories unchanged.
- **Future icon track:** mod-added categories via CCK or other organizers may ship icons in **mod metadata** (not `PartCategories.cfg`) — when CCK tab indexing lands, icon resolution may need a separate metadata/icon-loader path alongside the stock button lookup.
- CCK is a **permanent ModTest base layer** (`apply-profile.ps1` junctions `CommunityCategoryKit` on all profiles) but **CCK custom tabs are not indexed** — only stock-derived categorizer metadata is.
- Main install uses CCK as primary organizer (~118 mod folders); `profile-vab-ui-communitycategorykit` is P0 in the compatibility matrix.

**CCK integration challenges:**

1. **API surface** — CCK exposes custom VAB tabs via mod registration. Need to determine:
   - Public types in `CCK.dll` (category definitions, filter delegates, tab metadata).
   - Whether categories are static at load or dynamic (mod load order, MM patches).
   - Event/callback when CCK rebuilds its tab list.
2. **Reflection fallback** — if no stable API, use `tools/ReflectSearch/` against `CCK.dll` + `Assembly-CSharp.dll` to trace tab click → part filter pipeline (same approach as thumbnail Phase 0 research).
3. **Filter identity** — each indexable tab needs a stable `FilterKey` (CCK category id, internal name, or hash of predicate) for dedup and history.
4. **Apply mechanism** — likely one of:
   - Call CCK's public "select category" / "apply filter" API.
   - Set the same internal state CCK sets on tab click (reflection).
   - Harmony postfix on CCK tab handler to capture predicate for matcher registration (last resort).
5. **Subtitle counts** — `CountMatchingCckCategory` must match parts visible when user clicks the tab manually; verify on ModTest with mods that register CCK categories (e.g. Near Future, Restock+).
6. **StockSearchGuard** — custom CCK tab apply must use `ApplyCustomFilter` + Enter/Exit suppress like other non-stock-text filters.

**Dependency on CCK API / reflection:**

| Approach | When to use |
|----------|-------------|
| CCK public API | Preferred — check CCK source / docs for category enumeration and filter application |
| Reflection on CCK internals | If API insufficient but fields stable across CCK 1.x |
| Harmony capture | Only if tab predicates are opaque and must be observed at runtime |

**Generalization (non-CCK tab UIs):**

The same architecture should support other organizer mods (VABOrganizer, PartCatalog) via pluggable `ITabFilterProvider` or per-mod matchers — but **CCK is P0** because it is the user's main install organizer.

### Acceptance criteria (future)

- [ ] Typing a CCK tab display name (or alias) surfaces a suggestion row with correct `· N parts` subtitle.
- [ ] Clicking the row shows the same parts as clicking the CCK tab in the parts panel.
- [ ] `KSP.log` shows apply path with matched count === subtitle count.
- [ ] No blank filter / zero-part rows (`PartSuggestion.IsValid()` guard).
- [ ] `profile-vab-ui-communitycategorykit` smoke tests pass (V8 in `TEST_PROTOCOL.md` extended).

### Non-goals (v1 of CCK indexing)

- Indexing individual parts inside a CCK tab differently from stock part search (parts already covered by `SuggestionIndex`).
- Replacing or hiding CCK's native tab UI.
- Supporting organizer mods beyond CCK until CCK path is proven.

---

## 2. Subassembly search

**Target:** Later milestone — **v1.x or v2.0** depending on research complexity.

### User intent

Eventually search **player-made subassemblies** from the VAB/SPH parts list. Subassemblies are saved craft files the user can place as prefab groups from the stock subassembly browser (not individual parts).

### Phase description

| Phase | Goal |
|-------|------|
| **Research** | Map stock subassembly UI, persistence format, and placement flow. |
| **Data model** | Define what is indexed (subassembly title, folder, part count, tags?, thumbnail?). |
| **Index build** | Initial scan of `Saves/{save}/Subassemblies/` at editor load; thereafter **incremental, folder-scoped** updates only (see Index refresh policy). |
| **Suggestion UX** | New row kind (e.g. `SuggestionKind.Subassembly`) — likely text-first; thumbnail reuse from v0.8 track optional. |
| **Apply path** | Selecting a row triggers the same action as picking the subassembly in stock UI (open picker + select, or direct placement — TBD). |

### Research needed

**Stock types and UI (verify via ReflectSearch / ILSpy on `Assembly-CSharp.dll`):**

| Area | Questions |
|------|-----------|
| `EditorSubassembly` | Class responsibilities; link to craft file path, display name, icon |
| Craft files | Location under `Saves/{save}/Subassemblies/` (VAB vs SPH subfolders?); `.craft` format fields for subassembly metadata |
| Stock UI | Subassembly tab/panel in parts list; search/filter if any; click → placement workflow |
| Events | Editor load (initial scan); save/delete/rename — **incremental** `SubassemblySuggestionIndex` update only (never full suggestion-index rebuild) |

**Open design questions:**

1. **Apply semantics** — Should clicking a subassembly suggestion filter the parts list to "show subassembly entry" or immediately begin placement (like clicking in stock UI)?
2. **Cross-save scope** — Index subassemblies for current save only, or all saves?
3. **Modded subassemblies** — Parts missing in current install → exclude or show with warning?
4. **Matching** — Title substring only, or also index contained part names/mod names?
5. **Harmony / hook surface** — Does subassembly selection go through `PartCategorizer` search pipeline or a separate code path?

**Index refresh policy (explicit — do not full-refresh):**

Saving, deleting, or renaming a subassembly MUST **not** trigger a full suggestion-index rebuild. Refresh is **scoped to the subassembly folder only** — incremental add/update/remove of subassembly entries in `SubassemblySuggestionIndex`, never `SuggestionIndex`, `MetadataSuggestionIndex`, or `CategorizerSuggestionIndex`.

| Event | Index action |
|-------|--------------|
| **Editor load** | Scan `Saves/{current save}/Subassemblies/` (stock layout: separate **VAB** and **SPH** subfolders); build initial `SubassemblySuggestionIndex` — **editor entry only** (indexes for parts/metadata/categorizer are pre-built at save load per startup indexing architecture) |
| **Save** | Add or update **one** entry (keyed by craft file path + editor context) |
| **Delete** | Remove **one** entry |
| **Rename** | Update key/display name for **one** entry |

**Never** call full `SuggestionIndex` / `MetadataSuggestionIndex` / `CategorizerSuggestionIndex` rebuild on subassembly lifecycle events.

**Fallback (optional):** if stock save/delete/rename hooks are unavailable at implementation time, use a **debounced folder rescan** — still **subassembly-folder only** (`Saves/{save}/Subassemblies/` VAB/SPH), not a global index rebuild.

**Organic rule application:**

- Index: subassembly matches query if title (and agreed metadata) matches.
- Apply: same subassembly file / `EditorSubassembly` instance stock UI would select.
- Count: for subassemblies, "count" may be N/A or "1 craft" — define subtitle convention (e.g. `subassembly · 12 parts` using part count inside craft).

### Acceptance criteria (future)

- [x] Saved subassembly appears when typing its title (or partial title).
- [x] Click applies/opens the same subassembly stock UI would (`EditorPartList.TapIcon`).
- [x] **All Subassemblies** stock root tab searchable (`SuggestionKind.AllSubassemblies`, v0.8.3.12).
- [x] No suggestion for missing/deleted craft files (zero-part / invalid craft excluded).
- [x] VAB and SPH both supported (shared stock folder; optional `VAB/` / `SPH/` subfolders filtered by editor).

### Non-goals (initial subassembly milestone)

- Full-text search inside subassembly craft contents (part names) — optional follow-up.
- Creating/editing subassemblies from the dropdown.
- Subassembly thumbnails until v0.8 thumbnail track is stable.

---

## Relationship to other documents

```
v0.7.x  ── text-only stability, save-load indexing (v0.7.9.1), deferred editor UI hook
v0.8.x  ── search term expansion: CCK tabs, subassemblies (this document)
v0.9.0  ── limited personal rollout (ROLLOUT_PLAN.md Phase 1)
v1.0.0  ── public release + CKAN
v1.x    ── CCK tab indexing if not shipped in v0.8 (this document §1)
v1.x–2.0 ── Subassembly search (this document §2)
```

~~v0.8.0 thumbnail experiments~~ — **deferred/abandoned** (`V0.8.0_THUMBNAILS_PLAN.md`).

**Cursor rule:** [`.cursor/rules/koobal-search-engine-suggestions.mdc`](../../.cursor/rules/koobal-search-engine-suggestions.mdc) — universal index/apply checklist; future CCK tab section references this roadmap.

**Testing:** CCK work extends `profile-vab-ui-communitycategorykit`; subassembly work needs new test protocol section when research completes.

---

*Planning document only. No version bump; no plugin code changes.*

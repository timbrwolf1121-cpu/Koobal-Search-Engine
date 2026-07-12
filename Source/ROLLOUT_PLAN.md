# Koobal Search Engine — Rollout Plan

**Document version:** 2026-07-07  
**Mod:** Koobal Search Engine (`KoobalSearchEngine`) — *a Kerbalized play on Google search ("Koobal" = Kerbal + Google; same spirit as the prior "Koogle" name). Formerly Part Search Suggest.*  
**Author:** timbrwolf1121  
**Target KSP:** 1.12.0 – 1.12.99 (pinned to 1.12.5 in `.version`)  
**Rollout intent:** No external sharing until **v0.9.0+**; **v1.0** = fully publishable (CKAN + public release)

---

## Current state snapshot (v0.7.8.0a mod testing phase)

| Item | Location / value |
|------|------------------|
| Source | `Source/PartSearchSuggest/` (18 production `.cs` files + `tools/ReflectSearch/`) |
| GameData | `GameData/KoobalSearchEngine/` |
| Built DLL output | `GameData/KoobalSearchEngine/Plugins/KoobalSearchEngine.dll` (Release build via csproj) |
| **Deployed version** | **v0.7.8.0a** (csproj `<Version>` = **0.7.8.1**; KSP `.version` = **0.7.8**) |
| **Rollback baseline** | **v0.7.0** preserved at `GameData/KoobalSearchEngine/rollback/v0.7.0/` — see `Source/PartSearchSuggest/ROLLBACK.md` |
| Version file | `KoobalSearchEngine.version` — `0.7.8` during mod testing; bump to `0.8.0` when v0.8.0.0 thumbnails track starts |
| **Next experiment track** | **v0.8.0.0** thumbnails — see [`V0.8.0_THUMBNAILS_PLAN.md`](V0.8.0_THUMBNAILS_PLAN.md) |
| License declared | MIT in `.version` only — **no `LICENSE` file in GameData** |
| Homepage URL | `url = local` in `.version` |
| Harmony | **Runtime dependency** — `StockSearchGuard.cs` patches `BasePartCategorizer` / `PartCategorizer` via HarmonyLib; csproj references `GameData/000_Harmony/0Harmony.dll` |
| Test instance | `Kerbal Space Program - ModTest` (junctioned core, lean GameData) |
| Main install | ~118 versioned mod folders in GameData (heavy compatibility profile) |

### GameData folder structure (today)

```
GameData/KoobalSearchEngine/
├── KoobalSearchEngine.version
├── README.txt
├── Plugins/
│   └── KoobalSearchEngine.dll          # built artifact; exclude .pdb from releases
└── PluginData/
    └── History.cfg                    # user-local; DO NOT ship in release zips
```

### Source layout (not shipped in player zip)

```
Source/PartSearchSuggest/
├── PartSearchSuggest.csproj           # net472, outputs to GameData/Plugins/
├── Plugin.cs                          # KSPAddon bootstrap, calls StockSearchGuard.ApplyPatches()
├── EditorSearchHook.cs                # hooks PartCategorizer.searchField (TMP_InputField)
├── StockSearchGuard.cs                # Harmony patches (3 targets)
├── SearchDropdownPanel.cs, SuggestionIndex.cs, MetadataSuggestionIndex.cs, …
├── tools/ReflectSearch/               # dev-only reflection helper; exclude from release zip
├── ROLLOUT_PLAN.md                    # this file (source-side planning doc)
├── V0.8.0_THUMBNAILS_PLAN.md          # v0.8.0.0 thumbnail experiment phases (plan-only)
└── ROADMAP.md                         # post-v1.0 features: CCK tabs, subassemblies (plan-only)
```

### Documentation drift to fix before v0.9.0

- README still titled **"POC"** and lists **"No Harmony patches"** — contradicts `StockSearchGuard.cs`.
- README changelog includes **v0.7.8.0a** mod testing baseline; v0.7.0 rollback baseline documented in `ROLLBACK.md`.
- ModTest has deployed DLL (synced with v0.7.8.0a); rollback artifacts live under `rollback/v0.7.0/`.

---

## Version numbering convention

| Stage | Pattern | Example | Meaning |
|-------|---------|---------|---------|
| Internal beta | `0.x.y` | `0.7.0`, `0.8.3` | Active development; ModTest only |
| Pre-release candidate | `0.9.x` | `0.9.0`, `0.9.2` | Limited personal rollout to trusted tester(s) |
| Release candidate | `1.0.0-rc.N` | `1.0.0-rc.1` | Feature-frozen; CKAN staging / final QA |
| Stable | `1.0.0` | `1.0.0` | Public release + CKAN index |
| Post-1.0 | SemVer | `1.0.1`, `1.1.0` | Bugfix / feature per SemVer |

**Rules**

- Bump **all three** together on each release: `KoobalSearchEngine.version`, csproj `<Version>`, zip filename.
- Tag Git releases as `v{version}` (e.g. `v0.9.0`, `v1.0.0`).
- Beta suffix optional in zip name only (`PartSearchSuggest-0.9.0-beta.zip`); `.version` file uses plain `0.9.0`.
- Do **not** share builds externally while `< 0.9.0`.

### Intake-fix attempt suffixes (v0.7.1x)

When iterating on the intake fix after a v0.7.0 rollback, use letter suffixes in README/zip names and map to csproj fourth segment:

| User label | csproj `<Version>` | KSP `.version` |
|------------|-------------------|----------------|
| v0.7.1a | `0.7.1.1` | `0.7.1` |
| v0.7.1b | `0.7.1.2` | `0.7.1` |

See `ROLLBACK.md` for full lineage and restore steps.

### Mod testing phase suffixes (v0.7.8.0x) — active

During ModTest profile validation (Phase 2.0), use **v0.7.8.0(x)** letter suffixes for instance-specific conflict fixes. Keep KSP `.version` at **`0.7.8`** until the **v0.8.0.0** thumbnails track starts (then `.version` → `0.8.0`).

| User label | csproj `<Version>` | KSP `.version` | Typical trigger |
|------------|-------------------|----------------|-----------------|
| v0.7.8.0a | `0.7.8.1` | `0.7.8` | Index dump + testing framework baseline |
| v0.7.8.0b | `0.7.8.2` | `0.7.8` | profile-vab-ui-communitycategorykit conflict fix |
| v0.7.8.0c | `0.7.8.3` | `0.7.8` | Next instance-specific fix |
| … | `0.7.8.N` | `0.7.8` | Continue incrementing fourth segment per letter |

**Rules:** bump the **letter suffix** (and csproj fourth segment), not the minor version, for profile-specific fixes. Record tested profile ↔ build in `testing/profiles/README.md`. See `ROLLBACK.md` for full mapping.

---

## Phase 0 — Internal hardening (now → v0.8.x)

**Goal:** Make ModTest and main install reliable enough that a single trusted tester won't hit obvious breakage.

### 0.1 ModTest instance checklist

- [ ] Sync built DLL from main → ModTest after every Release build:
  ```powershell
  dotnet build "Source/PartSearchSuggest/PartSearchSuggest.csproj" -c Release
  robocopy "GameData\KoobalSearchEngine" `
    "..\Kerbal Space Program - ModTest\GameData\KoobalSearchEngine" /MIR /XD obj
  ```
- [ ] Confirm ModTest has `000_Harmony/` (HarmonyKSP 2.2.1.0 present in main).
- [ ] Launch ModTest VAB + SPH; verify log lines from README § "Test it".
- [ ] Document ModTest CKAN CLI workflow (`Tools\ckan.exe`, instance `KSP-ModTest`) for adding conflict mods on demand.

### 0.2 Compatibility audit checklist (lean ModTest)

Run in **VAB and SPH** on ModTest (Squad + Harmony + Koobal Search Engine):

| Area | Test | Pass criteria |
|------|------|---------------|
| Bootstrap | Fresh editor load | `[Koobal] Editor scene detected`, `Indexed N loaded parts`, `Hooked native editor search field` |
| Dropdown UI | Type `engine`, click row | Dropdown visible, no ghosting, catalog slides back on dismiss |
| Precise part | Click part row | `ApplyPrecisePart: exact filter for '...'`; single part shown |
| Author/mod | Type partial author (e.g. co-author from AviationLights CKAN data) | Author row + filter via `ApplyModAuthorFilter` |
| Categorizer | Type `fuel tank`, `rockomax` | Categorizer rows with `function · N parts` subtitles |
| History | Clear bar, refocus | Recent searches from `PluginData/History.cfg` |
| Harmony guard | Apply author filter, then type in search box | Stock `SearchFilter_` blocked when custom filter active (log lines present) |
| CKAN authors | ModTest with CKAN registry | `[Koobal] CKAN registry` enrichment or graceful skip log |

### 0.3 Codebase / packaging hygiene (no feature work required)

- [ ] Align version to **0.8.0** when Phase 0 completes (optional milestone).
- [ ] Update README: remove "POC" framing, document Harmony2 dependency, update changelog.
- [ ] Add `GameData/KoobalSearchEngine/LICENSE` (MIT full text).
- [ ] Add `CHANGELOG.md` (move version history out of README).
- [ ] Replace `url = local` with placeholder GitHub/SpaceDock URL (even if repo private until v1.0).
- [ ] Add `.gitignore` / packaging exclude list: `*.pdb`, `PluginData/History.cfg`, `tools/`.
- [ ] Create `build-release.ps1` (build + stage zip contents — script only, no plugin changes).

### 0.4 Known technical blockers / risks (from codebase)

| Blocker | Severity | Notes |
|---------|----------|-------|
| **Harmony2 hard dependency** | High | Must be installed (`000_Harmony`); declare `depends: Harmony2` for CKAN; do not bundle Harmony in release zip |
| **Harmony patch surface** | High | Patches `SearchField_OnValueChange`, `SearchStart`, `SearchFilterResult` — conflicts with any mod patching same methods |
| **KSP version pin** | Medium | `.version` max `1.12.99`; untested on 1.11/1.10; decide scope for v1.0 |
| **Editor UI fragility** | Medium | Heavy reliance on `EditorPanels`, `PartCategorizer`, reflection in `PartsPanelCollapseHelper` — KSP minor updates can break layout |
| **CKAN registry optional feature** | Low | Author enrichment reads `CKAN/registry.json`; non-CKAN installs degrade gracefully |
| **No keyboard nav** | Low (UX) | Documented limit; not a rollout blocker unless tester expects it |
| **User data in History.cfg** | Medium (privacy) | Must exclude from all distribution zips |
| **Squad plugin rules** | High (v1.0) | Public plugin releases require **source code publicly available** (GitHub or bundled) per KSP add-on posting rules |

---

## Phase 0.5 — v0.8.x search term expansion (replaces thumbnail track)

**Goal:** Expand what Koobal can suggest and apply — CCK tabs, subassemblies, other VAB surfaces — per [`ROADMAP.md`](ROADMAP.md).  
**Deferred:** Thumbnail experiments ([`V0.8.0_THUMBNAILS_PLAN.md`](V0.8.0_THUMBNAILS_PLAN.md)) — load cost on ~150-mod installs outweighs benefit.

**v0.7.9.0 optimization:** Deferred metadata/categorizer indexing — `Search ready (basic)` ~1–3 s, `Search ready (full)` ~8–15 s on main install.

~~Phase 0.5 — v0.8.0.0 thumbnail experiments (before v0.9.0)~~ — see archived plan above.

---

## Phase 1 — v0.9.0 limited personal rollout

**Goal:** Share a clean manual zip with one trusted tester; collect structured feedback.  
**Gate:** All Phase 0 pre-rollout QA items pass on ModTest **and** a smoke test on main install.

### 1.1 Pre-rollout QA checklist

**Build & package**

- [ ] `dotnet build … -c Release` succeeds with zero warnings worth fixing
- [ ] DLL timestamp matches release tag
- [ ] Zip contains only files listed in § "Zip package contents" below
- [ ] Zip extracts to correct `GameData/KoobalSearchEngine/` layout (no extra nesting)
- [ ] No `History.cfg`, no `.pdb`, no `tools/`, no `obj/`

**Functional (VAB + SPH, stock parts only + Harmony2)**

- [ ] All items in Phase 0.2 compatibility audit pass
- [ ] 30-minute editor session without NullReferenceException in `KSP.log`
- [ ] Save/load while in editor does not duplicate hooks or leak overlay

**Functional (tester's mod profile — agree upfront)**

- [ ] Tester confirms Harmony2 version (CKAN-managed or manual 000_Harmony)
- [ ] Tester runs agreed query set (see § Tester instructions)
- [ ] Tester reports conflicts with any editor UI mods they use

**Documentation**

- [ ] README install section written for **end user** (not developer)
- [ ] CHANGELOG.md includes v0.9.0 section
- [ ] LICENSE file included
- [ ] Dependency callout: **Requires Harmony2 (HarmonyKSP)** — install via CKAN or [HarmonyKSP releases](https://github.com/KSPModdingLibs/HarmonyKSP/releases)

### 1.2 Zip package contents (exact structure)

**Filename:** `PartSearchSuggest-0.9.0.zip`

```
PartSearchSuggest-0.9.0.zip
└── GameData/
    └── PartSearchSuggest/
        ├── PartSearchSuggest.version
        ├── README.txt
        ├── CHANGELOG.md
        ├── LICENSE
        └── Plugins/
            └── PartSearchSuggest.dll
```

**Include**

| Path | Reason |
|------|--------|
| `Plugins/PartSearchSuggest.dll` | Plugin |
| `PartSearchSuggest.version` | KSP Version Check / CKAN compatibility |
| `README.txt` | Install + usage + dependency |
| `CHANGELOG.md` | Tester-visible release notes |
| `LICENSE` | Required for public distribution; good practice for trusted share |

**Exclude**

| Path | Reason |
|------|--------|
| `PluginData/History.cfg` | User-specific search history |
| `*.pdb` | Debug symbols |
| `000_Harmony/` | Separate dependency — avoids version conflicts |
| `Source/` | Ship via GitHub for v1.0; optional link in README for v0.9.0 |
| `tools/ReflectSearch/` | Dev-only |
| `ROLLOUT_PLAN.md` | Internal planning |

**Zip layout note:** Top-level folder must be `GameData/` (SpaceDock/CKAN convention). Tester merges into KSP root, **not** into `GameData/GameData/`.

### 1.3 Tester install instructions (step-by-step)

Send tester this sequence (adapt paths):

1. **Prerequisites**
   - Kerbal Space Program **1.12.x** (match `ksp_version_min` / `max` in `.version`).
   - **Harmony2** installed: CKAN → install `Harmony2`, *or* extract [HarmonyKSP release](https://github.com/KSPModdingLibs/HarmonyKSP/releases) so `GameData/000_Harmony/` exists.

2. **Back up** existing `GameData/KoobalSearchEngine/` if present.

3. **Install PartSearchSuggest**
   - Download `PartSearchSuggest-0.9.0.zip`.
   - Extract so `{KSP}/GameData/KoobalSearchEngine/Plugins/PartSearchSuggest.dll` exists.
   - Do **not** copy `History.cfg` from anyone else's install.

4. **Verify**
   - Launch KSP → VAB or SPH.
   - Open `%LOCALAPPDATA%Low\Squad\Kerbal Space Program\Player.log` or `{KSP}/KSP.log`.
   - Confirm:
     - `[Koobal] Editor scene detected — starting hook.`
     - `[Koobal] Indexed N loaded parts.`
     - No Harmony exception stack traces on startup.

5. **Smoke test queries** (type in stock search bar)

   | Query | Expected |
   |-------|----------|
   | `engine` | Categorizer and/or part rows; subtitle shows match reason |
   | `rockomax` | Manufacturer categorizer row or parts |
   | `titan` | Part title matches |
   | Partial mod author | Author row with `author · N parts` if mod has author metadata |
   | Click part row | Only that part visible; log shows `ApplyPrecisePart` |
   | Escape / click dim overlay | Dropdown closes; parts catalog returns |

6. **Feedback loop**
   - Tester fills short report template (provide with zip):
     - KSP exact version, Harmony2 version, mod count / CKAN metapackage name
     - Pass/fail per smoke test row
     - `KSP.log` excerpt for any failure (search `[Koobal]`)
     - Screenshot of UI issues (ghosting, misalignment, invisible dropdown)
   - Developer triages → patch → **v0.9.1** zip if needed before Phase 2.

### 1.4 Feedback / issue tracking (v0.9.0)

- Use private GitHub repo Issues **or** shared doc — avoid public forum until v1.0.
- Label issues: `blocker`, `compatibility`, `ux`, `performance`.
- **Blocker definition:** crash, search completely broken, or parts list stuck hidden after dropdown.

---

## Phase 2 — v0.9.x compatibility testing (heavy mod lists)

**Goal:** Validate on realistic installs before v1.0 CKAN submission.

### 2.0 Mod matrix testing (v0.7.8–v0.9.0) — `testing/` framework

Infrastructure: `Source/PartSearchSuggest/testing/`

| Artifact | Purpose |
|----------|---------|
| `MAIN_INSTALL_MODS.md` | Ranked inventory; **VAB-UI / ORGANIZER** section (P0) |
| `apply-profile.ps1` | CKAN install + junction from main; one organizer per profile |
| `TEST_PROTOCOL.md` | VAB + organizer checklists |
| `TEST_RUN_WORKFLOW.md` | 3-step agent workflow: loaded report → conflict check → test briefing |
| `parse-test-log.ps1` | Automated KSP.log regression scan |
| `profiles/profile-*` | CKAN-primary mod lists; deps auto-resolved |

**Test order (mandatory):**

1. `profile-00-baseline`
2. VAB-UI profiles — **one organizer at a time** (never bundle VABOrganizer + PartCatalog + CCK):
   - `profile-vab-ui-communitycategorykit` (**user's main install organizer**)
   - `profile-vab-ui-kspcommunityfixes`
   - `profile-vab-ui-hangar`, `profile-vab-ui-fshangarextender`
   - `profile-vaborganizer` (CKAN-only; not on main)
3. Parts sweeps: `profile-01-main-top10` → `profile-02` → `profile-03`
4. `profile-05-legacy` as needed

**CKAN policy:** Use `ckan install --instance KSP-ModTest --headless <Mod>` with default dependency resolution. CKAN metadata is the source of truth for KSP version compatibility. Junction `GameData/<folder>` from main when the mod is already present; CKAN fills gaps.

**v0.7.8 index dump:** `GameData/KoobalSearchEngine/PluginData/DebugSettings.cfg` → `dumpIndexStats = true` logs `IndexStats:` lines on first editor index build.

### 2.1 Test matrix

| Profile | Instance | Mod count | Purpose |
|---------|----------|-----------|---------|
| **Minimal** | ModTest | ~3 mods + Squad | Regression baseline |
| **Medium** | ModTest + CKAN installs | ~15–30 mods | Editor UI + MM interaction |
| **Heavy** | Main install | ~118 versioned folders (~150 total with unversioned) | Real-world compatibility |

**KSP versions** (pick scope for v1.0 — recommend 1.12.3–1.12.5 minimum tested)

| KSP | Priority | Notes |
|-----|----------|-------|
| 1.12.5 | P0 | Primary dev version |
| 1.12.3 | P1 | KSPCommunityFixes FasterEditorPartList targets 1.12.3+ |
| 1.12.0 | P2 | `.version` min |
| 1.11.x | P3 | Only if expanding `ksp_version_min` |

**Harmony dependency**

| Setup | Test |
|-------|------|
| CKAN `Harmony2` | Default path for v1.0 users |
| Manual `000_Harmony` 2.2.1.0 | Match HarmonyKSP current |
| Missing Harmony | Must fail gracefully with clear log (verify before release) |

### 2.2 Compatibility mods to test against

Prioritized by **conflict likelihood** with PartSearchSuggest's hook surface (`PartCategorizer.searchField`, editor panels, Harmony patches):

| Priority | Mod (CKAN id) | Why |
|----------|---------------|-----|
| P0 | **Harmony2** | Hard dependency |
| P0 | **CommunityCategoryKit** | **User's main install organizer** — custom VAB tabs; test first via `profile-vab-ui-communitycategorykit` |
| P0 | **VABOrganizer** | Subcategory drawer (CKAN-only on user's install); `profile-vaborganizer` |
| P0 | **KSPCommunityFixes** | `FasterEditorPartList` patches editor search/category performance |
| P0 | **ModuleManager** | Present on all heavy installs |
| P1 | **FShangarExtender** | Alters hangar/editor UI layout |
| P1 | **Hangar** | Editor/storage UI overlay |
| P1 | **000_ClickThroughBlocker** | Global click-through / overlay behavior |
| P1 | **001_ToolbarControl** | Editor toolbar stacking |
| P1 | **B9PartSwitch** | Huge part count + variant names (search index stress) |
| P1 | **ReStock** + **ReStockPlus** | Part title/tag changes |
| P2 | **MechJeb2** | Common; verify no editor startup conflict |
| P2 | **TweakScale** | High part count |
| P2 | **CommunityCategoryKit** | Moved to P0 — see VAB-UI profiles above |
| P2 | **ExtraplanetaryLaunchpads** / **KIS** | Editor workflow mods |
| P2 | **WildBlueIndustries** suite | Many parts, `.version` author metadata |
| P3 | **AviationLights** | CKAN author enrichment test case (already in ModTest) |
| P3 | **BonVoyage** | ModTest baseline |

**Explicit conflict watch:** any future mod that replaces or hides the **stock** `PartCategorizer.searchField` or patches the same Harmony methods.

### 2.3 Phase 2 exit criteria (ready for v1.0.0-rc.1)

- [ ] Heavy profile: 2+ hour editor session without critical failures
- [ ] All P0/P1 mods tested pass smoke tests in VAB **and** SPH
- [ ] No unresolved `blocker` issues
- [ ] Performance acceptable: index build logged once per editor session; typing latency subjectively instant on heavy install
- [ ] v0.9.x changelog documents all tester-found fixes

---

## Phase 3 — v1.0 public release

**Goal:** Publishable package indexable on CKAN with SpaceDock/GitHub release assets.

### 3.1 Release channel checklist

| Channel | Action |
|---------|--------|
| **GitHub** | Public repo; tagged releases with zip asset; full `Source/` |
| **SpaceDock** | Mod page; upload zip; link GitHub; specify KSP 1.12 |
| **Forum** | Optional KSP forum release thread (license + dependencies + source link) |
| **CKAN** | NetKAN PR to `KSP-CKAN/NetKAN` |

### 3.2 v1.0 package additions vs v0.9.0 zip

- [ ] Public GitHub repository URL in `.version` `url` field
- [ ] `CHANGELOG.md` complete through v1.0.0
- [ ] `LICENSE` (MIT)
- [ ] Source code public (GitHub) — **required** for KSP plugin distribution rules
- [ ] Optional: `PartSearchSuggest-1.0.0-source.zip` or tag-only source
- [ ] SpaceDock mod image + short abstract
- [ ] Support channel documented (GitHub Issues preferred)

### 3.3 CKAN metadata draft

**File:** `NetKAN/PartSearchSuggest.netkan` (submitted via PR to [KSP-CKAN/NetKAN](https://github.com/KSP-CKAN/NetKAN))

```json
{
    "spec_version": "v1.8",
    "identifier": "PartSearchSuggest",
    "name": "Part Search Suggest",
    "abstract": "Predictive dropdown on the native VAB/SPH parts search bar with metadata-aware suggestions (authors, mods, categorizer filters).",
    "author": "timbrwolf1121",
    "license": "MIT",
    "version": "1.0.0",
    "ksp_version_min": "1.12.0",
    "ksp_version_max": "1.12.99",
    "depends": [
        { "name": "Harmony2" }
    ],
    "tags": [
        "plugin",
        "editor"
    ],
    "resources": {
        "homepage": "https://spacedock.info/mod/XXXX/PartSearchSuggest",
        "repository": "https://github.com/YOUR_USER/PartSearchSuggest",
        "bugtracker": "https://github.com/YOUR_USER/PartSearchSuggest/issues"
    },
    "$kref": "#/ckan/github/YOUR_USER/PartSearchSuggest",
    "install": [
        {
            "find": "GameData/PartSearchSuggest",
            "install_to": "GameData"
        }
    ]
}
```

**CKAN notes**

- Use **`Harmony2`** identifier (HarmonyKSP / `000_Harmony`) — do **not** bundle Harmony in the mod zip; CKAN installs dependency separately ([HarmonyKSP guidance](https://github.com/KSPModdingLibs/HarmonyKSP)).
- `$kref` assumes GitHub releases with zip containing top-level `GameData/KoobalSearchEngine/`.
- SpaceDock `$kref` alternative: `"$kref": "#/ckan/spacedock/XXXX"` if primary hosting is SpaceDock.
- After merge, CKAN bot generates stamped `.ckan` files in CKAN-meta per release.
- Run local validation: `ckan.exe show PartSearchSuggest` / install into ModTest instance.

**Optional relationships**

```json
"suggests": [
    { "name": "KSPCommunityFixes", "comment": "Commonly co-installed; editor part-list performance" }
]
```

Do **not** hard-depend on KSPCommunityFixes — only Harmony2 is required today.

### 3.4 SpaceDock / forum / GitHub conventions

| Convention | PartSearchSuggest application |
|------------|-------------------------------|
| Zip root | `GameData/KoobalSearchEngine/...` |
| Version in filename | `PartSearchSuggest-1.0.0.zip` |
| License visible | MIT in zip + SpaceDock field + forum post |
| Dependencies listed | Harmony2 prominent in README first paragraph |
| Source link | GitHub public repo (Squad plugin rule) |
| Changelog | `CHANGELOG.md` + GitHub release notes |
| No bundled libs | Harmony, MM, etc. listed as dependencies only |
| `.version` file | Required for KSP AVC / Version Check mods |

### 3.5 Support channels (v1.0)

| Channel | Use |
|---------|-----|
| GitHub Issues | Bug reports, compatibility |
| GitHub Discussions (optional) | Usage questions |
| SpaceDock comments | Redirect to GitHub Issues |
| CKAN forum thread | Indexing / install issues only |

---

## Milestone summary

| Milestone | Version | Deliverable | Audience |
|-----------|---------|-------------|----------|
| M0 | 0.7.x | Text-only stability (v0.7.6 race fixes; v0.7.7 availability) | Developer only |
| M0.5 | **0.8.0.0** | Thumbnail experiment phases (parsed spikes; optional for v0.9.0) | Developer only |
| M0.9 | pre-0.9.0 | ModTest hardening, docs/license/changelog, packaging script | Developer only |
| M1 | **0.9.0** | Manual zip + tester instructions + feedback template (text-first; thumbnails if Phase 4 done) | **One trusted tester** |
| M2 | 0.9.x | Fixes from tester + heavy-mod compatibility matrix | Tester + developer |
| M3 | 1.0.0-rc.1 | Feature freeze; CKAN netkan PR draft | Staging |
| M4 | **1.0.0** | GitHub + SpaceDock release; CKAN indexed | Public |
| M5 | **1.x** | CCK tab indexing — custom VAB tabs searchable/suggestable | Public |
| M6 | **1.x–2.0** | Subassembly search — player craft files from parts list | Public |

### Post-v1.0 feature roadmap

After **v1.0** (or late **v0.9.x** if promoted), see [`ROADMAP.md`](ROADMAP.md):

| Feature | Version band | Summary |
|---------|--------------|---------|
| **CCK tab indexing** | v1.x | Community Category Kit custom tabs (and similar parts-list tab filters) indexed and applied via the same organic index/apply rule as stock categorizer filters |
| **Subassembly search** | v1.x or v2.0 | Search player-made subassemblies (saved craft files); research needed on `EditorSubassembly`, craft persistence, stock UI |

These are **not** v1.0 blockers. Thumbnail experiments (v0.8) and compatibility matrix (v0.9.x) take priority.

---

## Appendix A — Build command reference

```powershell
# From KSP root
dotnet build "Source/PartSearchSuggest/PartSearchSuggest.csproj" -c Release

# Sync to ModTest (mirror GameData folder, exclude build cruft)
robocopy "GameData\KoobalSearchEngine" `
  "..\Kerbal Space Program - ModTest\GameData\KoobalSearchEngine" /MIR /XF *.pdb
```

## Appendix B — Log lines to grep during QA

```
[Koobal] Editor scene detected
[Koobal] Indexed
[Koobal] Hooked native editor search field
[Koobal] ApplyPrecisePart
[Koobal] ApplyModAuthorFilter
[Koobal] ApplyCategorizerFilter
[Koobal] PartsPanelCollapse
[Koobal] Blocked stock SearchFilter_
HarmonyException
```

## Appendix C — Top pre-v0.9.0 gaps (action list)

1. **Harmony dependency undocumented** — README claims no Harmony; code requires Harmony2 + patches stock search pipeline.
2. **ModTest missing built DLL** — test instance cannot validate plugin until sync/deploy step is routine.
3. **No LICENSE / CHANGELOG / public URL** — blocks even trusted rollout quality bar and v1.0 compliance.
4. **Zero compatibility testing on heavy mod list** — README admits untested; KSPCommunityFixes + editor UI mods are high risk.
5. **Version & branding drift** — still at v0.6.9 on disk / "POC" README while targeting v0.7.0 beta → v0.9.0 rollout.

---

## Rights & Permissions Audit (pre-v0.9.0)

**Full report:** [`RIGHTS_AUDIT.md`](RIGHTS_AUDIT.md) (2026-07-06)  
**Verdict:** **YELLOW** — safe for limited trusted-tester rollout once P0 items are done. **No RED blockers.**

### Summary

| Area | Status |
|------|--------|
| Source (18 production `.cs` files) | **GREEN** — original code; standard Harmony/reflection patterns; no copied mod or decompiled paste |
| Dependencies | **GREEN** — `Private=false` on all refs; ships only `KoobalSearchEngine.dll`; Harmony2 MIT, not bundled |
| GameData assets | **GREEN** — no bundled textures/fonts/audio; runtime `Texture2D.whiteTexture` + stock TMP only |
| Runtime data (CKAN registry, `.version`, parts) | **GREEN** — read-only from user's install; nothing redistributed |
| License / docs | **YELLOW** — MIT in `.version` only; no `LICENSE` file; README falsely claims "No Harmony patches" |
| v1.0 Squad policy | **YELLOW** — public source required for public plugin distribution; Harmony on stock editor is standard |

### v0.9.0 zip ship list (rights-checked)

| Include | Exclude |
|---------|---------|
| `Plugins/KoobalSearchEngine.dll` | `*.pdb` |
| `KoobalSearchEngine.version` | `PluginData/History.cfg` |
| `README.txt` (after Harmony fix) | `000_Harmony/`, `Source/`, `tools/` |
| `LICENSE` (MIT — **add before share**) | `obj/`, other mods' files |
| `CHANGELOG.md` (recommended) | |

### P0 action items (before any external share)

1. Add `GameData/KoobalSearchEngine/LICENSE` (full MIT text).
2. Fix README: document **Harmony2** hard dependency; remove "No Harmony patches" claim.
3. Enforce release zip excludes `.pdb` and `History.cfg`.
4. README install section: link [HarmonyKSP](https://github.com/KSPModdingLibs/HarmonyKSP/releases) or CKAN `Harmony2`.

### P1 (v1.0 public release)

- Public GitHub source (Squad add-on rule §5).
- CKAN `depends: Harmony2` + `"license": "MIT"`.
- Optional `[assembly: KSPAssemblyDependency("0Harmony", …)]`.

---

*Planning document only. No plugin code changes were made as part of this rollout plan.*

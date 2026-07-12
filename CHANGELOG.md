# Changelog

Koobal Search Engine — developer history (not shipped in the player GameData README).

## v0.8.5.2a — SpaceDock / CKAN Express packaging

- Player label **0.8.5.2a**; assembly / AVC BUILD **0.8.5.3** (letters not allowed in AssemblyVersion / AVC integers).
- Short player README; LICENSE + BrandingSettings in GameData package.
- Internal `KoobalSearchEngine.ckan` with `depends: Harmony2` for Express Mode indexing.
- Dual-deploy Main + ModTest; player zip `KoobalSearchEngine_v0.8.5.2a.zip` on Desktop\KSE.
- No 0.8.6 suggestion-research changes (still parked).

## v0.8.5.2 — branding footer + organic apply race restore

- **Apply fix:** clicking a part suggestion (e.g. Z-100) or categorizer/mod row no longer re-runs loose stock `PartMatchesSearch` and floods the list with unrelated parts. Index predicate === apply predicate is preserved after blur.
- **Cause (lost v0.7 / v0.8.5.1 regression):** `ApplyPrecisePart` / categorizer custom filters still applied correctly, but `StockSearchGuard` cleared the active custom filter on stock `SearchStart` (blur after click), letting stock overwrite the tight result.
- **Change:** block void `SearchStart` while apply-suppressed **or** a Koobal custom filter is active; typing still clears the guard via `CancelPendingStockSearchForTyping`. Do not skip `SearchRoutine`.
- **Branding:** empty-query Koobal wordmark no longer left-aligns when recent-search history rows are present, or when the dropdown resizes/repositions.

## v0.8.5.1-beta — suggestion quality rebalance

- Rich mix restored — function/category/manufacturer/diameter, mod/author/suite, and part titles sit above parts in ranking.
- Flat quality-sorted categorizer budget (top 8 by RankScore); synonym-tag denylist; FilterResource no longer tokenizes free-form `resourceInfo` prose.
- Part scoring: query length ≤ 2 → title/name-first; length ≥ 3 → tag-weighted within the parts pool.

## v0.8.5.1-beta — SearchStart NRE fix + hangar-free index load

- After applying a suggestion, typing no longer throws `NullReferenceException: routine is null` from stock `SearchStart`.
- Stop skipping `SearchRoutine`; block void `SearchStart` only while a Koobal apply is in progress.
- Editor no longer starts index builds; `GameLoadBootstrap` is the sole builder.
- Larger dropdown fonts / contrast; centered branding footer; suggestion clicks record history; programmatic clear-history trash icon.

## v0.8.5.0-beta — optimization + sanitary beta pass

- Hot-path performance cleanup; verbose logging gated behind `verbose = true`.
- Clean default history; package hygiene (DLL, `.version`, README, BrandingSettings).

## v0.8.4.0 — core-only re-baseline

- Stripped back to the stable v0.7.x core; removed subassembly + custom-category search and invasive stock-UI Harmony patching. Stock UI remains native.

# SpaceDock Express Mode — Koobal Search Engine

CKAN wiki (authoritative):  
https://github.com/KSP-CKAN/CKAN/wiki/Adding-a-mod-to-the-CKAN#express-mode

**Preferred path for this mod:** upload to SpaceDock and check **Add to CKAN**.  
Do **not** open a NetKAN PR unless SpaceDock Express fails and CKAN asks for one.

## What Express Mode does

1. You upload a clean GameData zip to SpaceDock.
2. You check the **Add to CKAN** / CKAN checkbox on the mod page.
3. That notifies the CKAN team to index the mod.
4. They review packaging / metadata; unless they need questions, you are done.

No NetKAN YAML PR is required for the happy path.

## Version scheme (v0.8.5.2a)

| Surface | Value | Why |
|---------|-------|-----|
| Zip / SpaceDock version / README / log banner | **0.8.5.2a** | Player-facing label (letter build) |
| Assembly / FileVersion | **0.8.5.3** | .NET `AssemblyVersion` cannot use letters |
| `KoobalSearchEngine.version` (AVC) | `MAJOR 0 / MINOR 8 / PATCH 5 / BUILD 3` | AVC requires integer VERSION fields only |

AVC does **not** accept `"VERSION": "0.8.5.2a"` as a string. CKAN/`$vref: ksp-avc` reads the numeric object. SpaceDock’s own version field should still be typed as **`0.8.5.2a`**.

## Player zip to upload

Path (handoff drop):

`C:\Users\timbr\Desktop\KSE\KoobalSearchEngine_v0.8.5.2a.zip`

Must unzip to:

```text
GameData/
  KoobalSearchEngine/
    Plugins/KoobalSearchEngine.dll
    PluginData/BrandingSettings.cfg
    KoobalSearchEngine.version
    KoobalSearchEngine.ckan      ← internal depends for CKAN
    LICENSE
    README.txt
```

**Do not include:** `.pdb`, `History.cfg`, `PartPopularity.cfg`, branding PNGs, source trees.

Manual install test: extract so `GameData/KoobalSearchEngine/` merges into a KSP `GameData/` (correct depth — not nested double GameData).

## Click-by-click: first SpaceDock upload

1. Create / log into https://spacedock.info
2. **Add a mod** (or open your existing Koobal page).
3. Fill:
   - **Name:** Koobal Search Engine  
   - **Short / abstract:** Predictive suggestions for the stock VAB/SPH parts search bar.  
   - **License:** MIT  
   - **Game:** Kerbal Space Program  
   - **KSP version:** 1.12.x (match AVC: min 1.12.0, tested 1.12.5)  
   - **Source / homepage:** https://github.com/timbrwolf1121-cpu/Koobal-Search-Engine  
4. Upload **`KoobalSearchEngine_v0.8.5.2a.zip`**.
5. Set release **version** to **`0.8.5.2a`** (exact player label).
6. Check **Add to CKAN** (wording may be “CKAN” / “List on CKAN”).
7. Publish / save.

### Depends — Harmony2 (critical)

SpaceDock’s checkbox alone does **not** always encode CKAN relationships. This package ships an **internal** `KoobalSearchEngine.ckan`:

```yaml
depends:
  - name: Harmony2
```

CKAN’s indexer merges internal `.ckan` properties into that version’s metadata (add-only). Identifier for Harmony on CKAN is **`Harmony2`** (folder `000_Harmony`).

**On the SpaceDock page**, if there is a Dependencies / Required mods UI:

- Add **Harmony 2** / **HarmonyKSP** (whatever SpaceDock lists for the CKAN id `Harmony2`).

**In the mod description**, also state clearly:

> Requires Harmony 2 (`GameData/000_Harmony/`). CKAN identifier: `Harmony2`.

If CKAN reviews and asks about depends, reply: depend on **`Harmony2`**; conflict/do-not-install with **Koobal Native Search** if they want a `conflicts` entry (optional — not required for first index).

Suggested CKAN identifier (if they ask): **`KoobalSearchEngine`** (matches GameData folder; letters/digits/dashes only).

## After publish checklist

- [ ] Zip downloads from SpaceDock and extracts to `GameData/KoobalSearchEngine/...`
- [ ] LICENSE present
- [ ] `.version` present (KSP 1.12.x min/max)
- [ ] No junk files in zip
- [ ] **Add to CKAN** checked
- [ ] Description mentions Harmony 2
- [ ] SpaceDock version string = `0.8.5.2a`
- [ ] Wait for CKAN bot / team (status: https://github.com/KSP-CKAN/CKAN/wiki/Adding-a-mod-to-the-CKAN — bot status page linked from wiki Troubleshooting)

## What you do **not** need

- NetKAN pull request (Express Mode is enough)
- Attaching SOURCE to the SpaceDock download
- Shipping suggestion-research / parked 0.8.6 work

## If Express stalls

Only then: open a CKAN “add mod” issue with the SpaceDock URL, or use the metadata webtool — still prefer letting the team fix netkan rather than maintaining a PR unless they request it.

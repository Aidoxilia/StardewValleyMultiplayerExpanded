# Player Romance (SMAPI 4.x, Stardew 1.6+)

Multiplayer player-to-player romance systems with host-authoritative state and save persistence.

## Core features

- Dating, marriage, pregnancy, child growth, farm worker AI.
- Carry system (with stamina recovery while carried and fatigued).
- Right-click player interaction menu (action buttons + disabled states).
- Romance Hub menu (`F7` by default).
- Incoming request popup with `Accept / Reject` buttons (dating, marriage, try-for-baby, carry, holding hands).

## Immersion Romance v2

- Immersive date sessions in `Town`, `Beach`, `Forest` (one global immersive session at a time).
- Temporary date stands (`IceCream`, `Roses`, `Clothing`) and temporary roaming NPCs during the session.
- Host-authoritative stand transactions (`gold + real item`) with anti-duplication request IDs.
- Heart system per couple (`HeartPoints`, `HeartLevel` up to 14 hearts).
- Town date is the baseline date and requires `0` hearts.
- Completed date reward is `+0.5 heart` (half a heart) per completed date.
- Holding hands consensual sessions (request/accept/reject/stop) with robust MP sync.

## Family & Stability v3

- Child presence is persistent in-world (farm/house/town routine), rebuilt on save load.
- Child visual profile is deterministic from both parents + child ID, with safe fallback.
- Social menu integration is vanilla-first: no custom tooltip overlay, player status + hearts are drawn directly in social rows, with player profile page on click.
- Child Interaction UX (vanilla-like): hover bubble icon + left-click actions (`Prendre soin`, `Jouer`, `Annuler`) and right-click feed with held food item.
- Feeding-driven growth: no passive growth when feeding system is enabled.
- Daily growth when fed is host-only deterministic (`+2` or `+3` years by config).
- Adult threshold tasks (`16+`): water, feed, collect, harvest, ship, fish.
- Holding hands hardening: movement clamp per tick + emergency distance stop + safe-position checks.
- Immersive date startup handshake: requested vs confirmed state, retry, and no cooldown/penalty on failed start.

## Installation

1. Build `PlayerRomance.csproj`.
2. Place this folder under `Stardew Valley/Mods/MultiplayerExpanded` (or your chosen mod folder name).
3. Ensure `SMAPI 4.x` and Stardew Valley `1.6+`.
4. Launch with SMAPI.

### One-click installer/updater (Windows)

- Project: `Installer/PlayerRomanceSetup.csproj`
- Build command:

```powershell
dotnet publish .\Installer\PlayerRomanceSetup.csproj -c Release
```

- Output folder:
  `Installer\bin\Release\net8.0-windows\publish\`
- Run:
  `PlayerRomanceSetup.exe` (from that folder)

If you want a fully single-file/self-contained executable, use:

```powershell
dotnet publish .\Installer\PlayerRomanceSetup.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

This requires internet access to fetch runtime packs during publish.

Flow:

1. User runs `PlayerRomanceSetup.exe`.
2. Installer auto-detects Stardew folder (`Steam/GOG/Xbox` common paths + registry + Steam libraries).
3. Installer downloads/updates runtime mod files from `https://github.com/Aidoxilia/StardewValleyMultiplayerExpanded` and installs to `Mods\MultiplayerExpanded`.
4. Installer creates desktop/start-menu shortcut that always launches through updater (`--launch`) before starting SMAPI.

Build command:

```powershell
dotnet build .\PlayerRomance.csproj -c Release /p:SkipModRootCopy=true
```

## Config (`config.json`)

### Immersion v2

- `EnableImmersiveDates`: enable immersive date sessions.
- `EnableHeartsSystem`: enable hearts progression and heart gating.
- `EnableHoldingHands`: enable holding hands system.
- `RomanceHubHotkey`: hotkey string for romance hub (default `F7`).
- `ChildrenManagementHotkey`: hotkey string for children management menu (default `F8`).
- `EnableVanillaSocialIntegration`: enables vanilla-style social row integration for player romance data.
- `HeartPointsPerHeart`: points per heart (default `250`).
- `MaxHearts`: max hearts (default `14`).
- `ImmersiveDatePointsReward`: legacy value (completion now uses half-heart reward).
- `RejectionHeartPenalty`: heart penalty on rejected consensual requests.
- `EarlyLeaveHeartPenalty`: heart penalty when immersive date ends early.
- `ImmersiveDateEndTime`: end-time threshold for completion reward.
- `HoldingHandsMinHearts`: minimum hearts to start holding hands.
- `ImmersiveDateMinHearts`: minimum hearts to start immersive date.
- `GiftsBonusMinHearts`: bonus threshold for stand gift heart bonus.
- `DuoBuffMinHearts`: reserved threshold for future duo buffs.
- `HoldingHandsBreakDistanceTiles`: break distance for holding hands session.
- `HoldingHandsSoftMaxDistanceTiles`: soft clamp distance used by spring-damper following.
- `HandsSpringStrength`, `HandsDamping`: smoothing parameters for holding hands movement.
- `HoldingHandsOffsetPixels`: follower side offset while holding hands.

### Family & Stability v3

- `EnableChildFeedingSystem`: enables feeding-based growth.
- `ChildYearsPerFedDayMin`, `ChildYearsPerFedDayMax`: deterministic growth range per fed day.
- `AdultWorkMinAge`: minimum age to run worker tasks.
- `EnableChildFishingTask`: allows fish task in child worker task set.
- `DateStartConfirmSeconds`: stable window before immersive date is confirmed.
- `DateStartRetryMaxAttempts`: max auto/manual retry attempts for immersive date start.
- `HandsMaxMovePixelsPerTick`: per-tick move clamp for holding hands follower.
- `HandsEmergencyStopDistanceTiles`: emergency stop threshold to avoid off-map/invisible desync.

### Modular mechanics (gated)

- `EnableCoupleSynergy`, `EnableRpInteractions`, `EnableRelationshipEvents`, `EnableCombatDuo`, `EnableChildEducationTraits`
- `EnableWakeupCuddleBuff`, `EnableLoveAura`, `EnableRegeneratorKiss`
- `LoveAuraRangeTiles`, `LoveAuraStaminaMultiplier`, `KissEnergyRestorePercent`
- Runtime behavior:
  - Wake-up cuddle: small morning stamina + heart bonus for close online couples.
  - Love aura: passive stamina trickle while partners are close.
  - Regenerator kiss: once/day close-range burst stamina + heart bonus.
  - RP interactions: periodic heart gain while actively holding hands.
  - Combat duo: stamina sustain while fighting together in mine/dungeon maps.
  - Child education trait: day-end bonus progression when child received both care + play.

### Existing systems

- `EnableCarry`, `CarryEnergyRegenPerSecond`, `CarryOffsetY`
- `EnableDateEvents`, `EnableMarriage`, `MarriageMinDatingDays`
- `EnablePregnancy`, `PregnancyDays`
- `EnableChildGrowth`, `EnableFarmWorker`, `AllowAdultChildWork`
- `EnableTaskWater`, `EnableTaskFeed`, `EnableTaskCollect`, `EnableTaskHarvest`, `EnableTaskShip`

## Commands

### Base

- `pr.propose <player>`
- `pr.accept` / `pr.reject`
- `pr.status [player]`
- `pr.date.start <player>` (force-start Town immersive date)
- `pr.date.start <dateId> [player]` (host force-start map date; auto-picks online partner if omitted)
- `pr.date.end` (force-end active immersive + map date runtime)
- `pr.marry.propose <player>`
- `pr.marry.accept` / `pr.marry.reject`
- `pr.marry.force <player>` / `force_marriage <player>` (host cheat)
- `pr.pregnancy.optin <player> [on/off]` (legacy/optional)
- `pr.pregnancy.try <player>`
- `pr.pregnancy.accept` / `pr.pregnancy.reject`
- `pr.pregnancy.force <player> [days]` (host cheat)
- `pr.pregnancy.birth <player>` (host cheat immediate baby)
- `pr.carry.request <player>`
- `pr.carry.accept` / `pr.carry.reject`
- `pr.carry.stop [player]`
- `pr.worker.runonce`
- `pr.child.age <childIdOrName> <days>`

### Hearts / Immersive / Hands (v2)

- `pr.hearts.status <player>`
- `pr.hearts.add <player> <delta>` (host debug)
- `pr.hearts.max <player>` / `max_hearth <player>` (host cheat: set max hearts)
- `pr.date.immersive.start <player> <town|beach|forest>`
- `pr.date.immersive.end`
- `pr.date.debug.spawnstands <town|beach|forest>` (host debug)
- `pr.date.debug.cleanup`
- `pr.date.immersive.retry`
- `pr.date.reset_state` (host debug force reset: active sessions + daily date lockouts)
- `pr.date.asset_test` / `date_asset_test` (host debug)
- `pr.date.warp_test` / `date_warp_test` (host debug: must warp to `Date_Beach`)
- `pr.date.markers_dump` / `date_markers_dump` (host debug)
- `pr.vendor.shop.open <ice|roses|clothing> [itemId] [offer]` (debug vendor buy/offer path)
- `romance help dev` (or `romance_help_dev`) to print the full dev command list
- `pr.hands.request <player>`
- `pr.hands.accept`
- `pr.hands.reject`
- `pr.hands.stop [player]`
- `pr.hands.debug.status`

### Child v3 debug/control

- `pr.child.feed <childIdOrName> [itemId]`
- `pr.child.feed.menu <childIdOrName>`
- `pr.child.interact <childIdOrName> <care|play|feed>`
- `pr.child.status [childIdOrName]`
- `pr.child.age.set <childIdOrName> <years>`
- `pr.child.age.set <years>` (fallback when exactly one child exists)
- `pr.child.task <childIdOrName> <auto|water|feed|collect|harvest|ship|fish|stop>`
- `pr.child.work.force <childIdOrName>`
- `pr.child.job.set <childIdOrName> <none|forager|crabpot|rancher|artisan|geologist>`
- `pr.child.dump <childIdOrName>`
- `pr.sim.morning` (host debug: simulate DayStarted systems + 06:10 pass)

### Extra debug commands

- `romance_status` (host only)
- `children_list` (host only)
- `synergy_test` (host only)
- `romance help dev` (full exhaustive dev command help)

### Debug dummy partner (solo)

- `pr.date.immersive.start DummyPartner <town|beach|forest>` force-starts a debug session in solo host mode.
- This uses a logical dummy partner flow for testing and does not require a real second client.

### Child Interaction UX (in-game)

- Hover a custom child NPC (`modData`: `PlayerRomance/ChildNpc`, `PlayerRomance/ChildId`) to show a talk icon.
- Left-click child to open interaction menu:
  - `Prendre soin`: contextual PG feedback.
  - `Jouer`: contextual PG feedback.
  - `Annuler`: closes menu.
- Right-click child while holding edible `(O)` food item: host validates and consumes exactly one item.
- Feeding remains host-authoritative (parent/host rights, online actor, valid food item, stack > 0, already-fed check).

## Multiplayer data/sync model

- Host only reads/writes save data (`ReadSaveData` / `WriteSaveData`).
- Clients request mutations via `ModMessage`.
- Host validates sender, state transitions, online presence, and cooldowns.
- Host applies mutation, persists, then broadcasts deltas/snapshot.

## Hearts progression guide

- Gain hearts:
  - complete a date (`+0.5 heart`);
  - offer gifts from immersive stands;
  - interact with immersive date NPCs (limited talk bonuses per session).
- Lose hearts:
  - reject a consensual request;
  - end immersive date early / break date conditions.
- Gating:
  - `Town` date: no heart requirement;
  - `Beach` and `Forest` immersive dates: require `ImmersiveDateMinHearts`.

## Validation scenarios (Host + 1 Client)

### 1) Immersive date start/end + cleanup

1. Put two players in relationship (`Dating+`).
2. Run `pr.date.immersive.start <partner> town`.
3. Verify temporary stands and temporary NPCs appear in location.
4. End with `pr.date.immersive.end` or wait until `ImmersiveDateEndTime`.
5. Verify cleanup: no lingering `PR_Date_...` NPCs/stands and session cleared.
6. Run map checks:

- `pr.date.asset_test`
- `pr.date.warp_test`
- `pr.date.markers_dump`

7. If any previous date state blocks testing, run `pr.date.reset_state` and retry immediately.

### 2) Stand buy/offer + sync + anti-dup

1. During immersive date, right-click near a stand.
2. Buy an item for self and offer one to partner.
3. Verify host-side gold deduction and real inventory transfer.
4. Verify no duplicate transfer for the same interaction request.

### 3) Hearts progression + gating

1. Run `pr.hearts.status <partner>` baseline.
2. Complete immersive date and offer gift.
3. Verify heart points/level increase.
4. Reject a request (`hands`, `dating`, etc.) and verify penalty.
5. Verify gating (`HoldingHandsMinHearts`, `ImmersiveDateMinHearts`).

### 4) Holding hands start/stop + break cases

1. Run `pr.hands.request <partner>` and accept.
2. Move together and verify follow sync.
3. Test breaks:
   - exceed break distance,
   - map mismatch,
   - peer disconnect,
   - manual `pr.hands.stop`.
4. Verify clean stop notification and no lingering session.

### 5) Child lifecycle + work

1. Force/trigger birth and verify child runtime NPC appears.
2. Reload save and verify child is reconstructed.
3. Feed child daily with `pr.child.feed`.
4. Verify age grows only when fed (`+2/+3` years by config).
5. At `16+`, assign work using `pr.child.task`.
6. Run `pr.worker.runonce` and verify parent-targeted reports and no obvious duplication.

### 8) Child interaction UX + feeding errors (Host + 1 Client)

1. Hover child NPC: verify talk icon appears only when local player can interact.
2. Left-click child: verify menu opens with `Prendre soin / Jouer / Annuler`.
3. Use `Prendre soin` and `Jouer`: verify contextual PG message and clean MP response.
4. Right-click child while holding edible food `(O)` item: verify host-only consume and snapshot sync.
5. Error cases:

- select non-edible item (rejected),
- no food in inventory,
- child already fed today,
- unauthorized player (not parent, not host).

### 6) Immersive start fallback/retry

1. Start immersive date and force an initial mismatch (e.g. map movement during startup).
2. Use `pr.date.immersive.retry`.
3. Verify failed startup does not consume daily date cooldown and does not apply early-leave penalty.

### 7) Non-regression

- Re-test dating, marriage, pregnancy, carry, child growth, worker runonce.
- Confirm old flows still function outside new v2 actions.

## Expected log categories

- `[PR.Core]`
- `[PR.Net]`
- `[PR.Data]`
- `[PR.System.Dating]`
- `[PR.System.Marriage]`
- `[PR.System.Pregnancy]`
- `[PR.System.ChildGrowth]`
- `[PR.System.Worker]`
- `[PR.System.Hearts]`
- `[PR.System.DateImmersion]`
- `[PR.System.HoldingHands]`
- `[PR.UI.RomanceHub]`
- `[PR.UI.ChildInteraction]`

## Troubleshooting

- `PlayerRomance.dll doesn't exist`: build the project first, then ensure `manifest.json` `EntryDll` matches.
- Desync suspicion: reconnect client, or trigger a host-side state change for snapshot rebroadcast.
- Host-only mutation errors: run debug mutation commands on host or use client request path.

## Known limits

- Temporary immersive NPCs are runtime placeholders from vanilla assets.
- Clothing stand uses vanilla wearable IDs; availability depends on local game item registry.
- Deep vanilla spouse internals are intentionally not patched for compatibility/stability.
- Child visual "mix" currently uses deterministic profile + vanilla template NPC assets (no custom sprite compositor yet).

## TODO

- Replace placeholder immersive NPC visuals with custom assets.
- Add richer scripted immersive date variants and branching dialogues.
- Improve worker pathing/obstacle logic.
- Add automated integration harness for MP flows.

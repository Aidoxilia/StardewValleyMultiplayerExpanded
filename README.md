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
- `HoldingHandsOffsetPixels`: follower side offset while holding hands.

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
- `pr.date.start <player>` (starts Town immersive date)
- `pr.marry.propose <player>`
- `pr.marry.accept` / `pr.marry.reject`
- `pr.pregnancy.optin <player> [on/off]`
- `pr.pregnancy.try <player>`
- `pr.pregnancy.accept` / `pr.pregnancy.reject`
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
- `pr.hands.request <player>`
- `pr.hands.accept`
- `pr.hands.reject`
- `pr.hands.stop [player]`

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

### 5) Non-regression
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

## Troubleshooting
- `PlayerRomance.dll doesn't exist`: build the project first, then ensure `manifest.json` `EntryDll` matches.
- Desync suspicion: reconnect client, or trigger a host-side state change for snapshot rebroadcast.
- Host-only mutation errors: run debug mutation commands on host or use client request path.

## Known limits
- Temporary immersive NPCs are runtime placeholders from vanilla assets.
- Clothing stand uses vanilla wearable IDs; availability depends on local game item registry.
- Deep vanilla spouse internals are intentionally not patched for compatibility/stability.

## TODO
- Replace placeholder immersive NPC visuals with custom assets.
- Add richer scripted immersive date variants and branching dialogues.
- Improve worker pathing/obstacle logic.
- Add automated integration harness for MP flows.

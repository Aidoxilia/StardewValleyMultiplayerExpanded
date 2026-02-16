# AGENTS.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

Player Romance is a SMAPI 4.x mod for Stardew Valley 1.6+ that adds multiplayer player-to-player romance systems. It uses a host-authoritative network model where the host owns all game state and clients request mutations via mod messages.

## Build Commands

```powershell
# Build mod (copies DLL to mod root for SMAPI to load)
dotnet build .\PlayerRomance.csproj -c Release

# Build mod without copying to root (CI/release builds)
dotnet build .\PlayerRomance.csproj -c Release /p:SkipModRootCopy=true

# Build Windows installer
dotnet publish .\Installer\PlayerRomanceSetup.csproj -c Release

# Build self-contained installer
dotnet publish .\Installer\PlayerRomanceSetup.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Testing

No automated test framework. Test manually in-game using console commands:
- Launch game with SMAPI
- Open console with `~` key
- Use `pr.*` commands (see Commands section in README.md)
- Test with host + client to verify multiplayer sync

## Architecture

### Entry Point
`ModEntry.cs` - Initializes all systems, handles save/load, provides shared helpers. All systems receive `ModEntry` reference for cross-system access.

### Multiplayer Data Flow
1. **Host** owns `HostSaveData` (`RomanceSaveData`), reads/writes via SMAPI `ReadSaveData`/`WriteSaveData`
2. **Clients** maintain `ClientSnapshot` (`NetSnapshot`) - read-only mirror of host state
3. Clients send mutation requests via `NetSyncService.SendToPlayer()` → Host validates → Host applies → Host broadcasts snapshot to all

### Net Layer (`Net/`)
- `MessageType.cs` - Enum of all message types (requests, decisions, state broadcasts)
- `MessageRouter.cs` - Routes incoming `ModMessageReceived` events to appropriate handler
- `HostHandlers.cs` - Processes client requests (validation, state mutation, snapshot broadcast)
- `ClientHandlers.cs` - Processes state broadcasts from host (applies to `ClientSnapshot`)
- `NetSyncService.cs` - Snapshot building, broadcasting, watchdog resync

### Systems (`Systems/`)
Each system handles one feature domain. Pattern: `{Feature}System.cs`
- `HandleX{Request}Host()` - Host-side validation and state mutation
- `RequestXFromLocal()` - Client-side request initiation
- Communicate via `NetSyncService.SendToPlayer()` / `NetSyncService.Broadcast()`
- Reference config via `mod.Config.{Setting}`

Key systems: `DatingSystem`, `MarriageSystem`, `PregnancySystem`, `ChildGrowthSystem`, `CarrySystem`, `HoldingHandsSystem`, `DateImmersionSystem`, `HeartsSystem`, `FarmWorkerSystem`

### Data Models (`Data/`)
- `RomanceSaveData.cs` - Host persistent state (relationships, pregnancies, children)
- `NetSnapshot.cs` - Serializable snapshot for client sync
- `ModConfig.cs` - Runtime config from `config.json`
- `*Record.cs` - Individual entity records (relationship, child, pregnancy, etc.)

### UI (`UI/`)
- `RomanceHubMenu.cs` - Main hub (F7 hotkey)
- `PlayerInteractionMenu.cs` - Right-click on player context menu
- `ConsentPromptMenu.cs` - Accept/Reject popup for incoming requests
- `RequestPromptService.cs` - Queue for consent prompts (prevents overlapping popups)

### Events (`Events/`)
- `GameEventOrchestrator.cs` - Subscribes to SMAPI events, dispatches to systems
- `CommandRegistrar.cs` - Registers console commands

## Conventions

### Logging
Use categorized log prefixes: `[PR.Core]`, `[PR.Net]`, `[PR.Data]`, `[PR.System.{Name}]`, `[PR.UI.{Name}]`

### Host Validation Pattern
All host handlers must:
1. Verify `senderId` matches message `FromPlayerId`
2. Verify both players online via `mod.FindFarmerById(id, includeOffline: false)`
3. Verify state preconditions
4. Send error via `mod.NetSync.SendError()` on failure

### State Mutation Pattern
```csharp
// 1. Modify HostSaveData
relationship.State = RelationshipState.Dating;

// 2. Mark dirty (flush immediately for critical changes)
mod.MarkDataDirty("Reason", flushNow: true);

// 3. Broadcast updated state
mod.NetSync.BroadcastSnapshotToAll();
```

### Pair Keys
Relationships use deterministic pair keys: `ConsentSystem.GetPairKey(idA, idB)` - always orders IDs consistently.

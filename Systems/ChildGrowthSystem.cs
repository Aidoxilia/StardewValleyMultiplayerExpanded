using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using xTile.Dimensions;
using SObject = StardewValley.Object;

namespace PlayerRomance.Systems;

public sealed class ChildGrowthSystem
{
    private const string ChildNpcFlagKey = "PlayerRomance/ChildNpc";
    private const string ChildNpcIdKey = "PlayerRomance/ChildId";
    private const string ChildNpcStageKey = "PlayerRomance/ChildStage";
    private static readonly Microsoft.Xna.Framework.Rectangle TalkIconSource = new(66, 4, 14, 12);
    private const int BabyMaxAgeYears = 3;
    private const int ChildMaxAgeYears = 11;
    private const int TeenMaxAgeYears = 15;

    // -------------------------------------------------------------------------
    // IA : états comportementaux
    // -------------------------------------------------------------------------
    private enum ChildAiState { Idle, Wander, GoHome }

    // Routine horaire : tranches de temps du jeu (Game1.timeOfDay)
    // Chaque tranche définit la location cible selon le stage
    private sealed class RoutineSlot
    {
        public int TimeStart { get; init; }   // ex: 600  = 6h00
        public int TimeEnd { get; init; }   // ex: 1200 = 12h00
        public string LocationChild { get; init; } = "FarmHouse";
        public string LocationTeen { get; init; } = "FarmHouse";
        public string LocationAdult { get; init; } = "Farm";
    }

    // Routine quotidienne — jours normaux (non-rainy)
    private static readonly RoutineSlot[] DailyRoutine =
    {
        new() { TimeStart = 600,  TimeEnd = 900,  LocationChild = "FarmHouse", LocationTeen = "FarmHouse", LocationAdult = "Farm"      },
        new() { TimeStart = 900,  TimeEnd = 1400, LocationChild = "Farm",      LocationTeen = "Farm",      LocationAdult = "Farm"      },
        new() { TimeStart = 1400, TimeEnd = 1800, LocationChild = "FarmHouse", LocationTeen = "Town",      LocationAdult = "Town"      },
        new() { TimeStart = 1800, TimeEnd = 2200, LocationChild = "FarmHouse", LocationTeen = "FarmHouse", LocationAdult = "FarmHouse" },
    };

    // Routine pluvieuse — tout le monde reste à l'intérieur
    private static readonly RoutineSlot[] RainyRoutine =
    {
        new() { TimeStart = 600,  TimeEnd = 2200, LocationChild = "FarmHouse", LocationTeen = "FarmHouse", LocationAdult = "FarmHouse" },
    };

    // Points d'ancrage par location — zones naturelles où errer
    // Le NPC choisira aléatoirement parmi ces ancres et pathfindera vers l'une d'elles
    private static readonly Dictionary<string, Vector2[]> AnchorsByLocation =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FarmHouse"] = new[]
            {
                new Vector2(5f,  4f),
                new Vector2(9f,  4f),
                new Vector2(5f,  8f),
                new Vector2(9f,  8f),
                new Vector2(7f,  6f),
                new Vector2(3f,  6f),
                new Vector2(11f, 6f),
            },
            ["Farm"] = new[]
            {
                new Vector2(58f, 12f),
                new Vector2(62f, 15f),
                new Vector2(66f, 12f),
                new Vector2(60f, 18f),
                new Vector2(64f, 18f),
                new Vector2(55f, 15f),
                new Vector2(68f, 15f),
            },
            ["Town"] = new[]
            {
                new Vector2(48f, 61f),
                new Vector2(52f, 63f),
                new Vector2(56f, 61f),
                new Vector2(50f, 65f),
                new Vector2(54f, 65f),
                new Vector2(44f, 63f),
                new Vector2(58f, 63f),
            },
        };

    private readonly ModEntry mod;
    private readonly Dictionary<string, string> runtimeNpcByChildId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> lastAppliedNpcSyncSequenceByChild = new(StringComparer.OrdinalIgnoreCase);

    // IA runtime (host seulement)
    private readonly Dictionary<string, ChildAiState> aiStateByChildId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> aiNextActionTickById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> aiPathFailCountById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vector2> aiCurrentAnchorById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> aiCurrentLocationById = new(StringComparer.OrdinalIgnoreCase);
    private int aiLastRoutineCheckTime = -1;

    public ChildGrowthSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Reset()
    {
        this.runtimeNpcByChildId.Clear();
        this.lastAppliedNpcSyncSequenceByChild.Clear();
        this.aiStateByChildId.Clear();
        this.aiNextActionTickById.Clear();
        this.aiPathFailCountById.Clear();
        this.aiCurrentAnchorById.Clear();
        this.aiCurrentLocationById.Clear();
        this.aiLastRoutineCheckTime = -1;
        if (!Context.IsWorldReady)
        {
            return;
        }

        this.RemoveAllRuntimeChildNpcs();
    }

    public void OnDayStartedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableChildGrowth)
        {
            return;
        }

        int day = this.mod.GetCurrentDayNumber();
        bool anyChanged = false;
        foreach (ChildRecord child in this.mod.HostSaveData.Children.Values)
        {
            this.EnsureV3Defaults(child);
            if (child.LastProcessedDay == day)
            {
                continue;
            }

            child.LastProcessedDay = day;
            int gainedYears = 0;
            if (this.mod.Config.EnableChildFeedingSystem)
            {
                if (child.IsFedToday)
                {
                    gainedYears = this.GetDailyGrowthYears(child, day);
                    child.AgeYears = Math.Max(0, child.AgeYears + gainedYears);
                    child.AgeDays = Math.Max(child.AgeDays, child.AgeYears * 7);
                    child.FeedingProgress++;
                }

                child.IsFedToday = false;
                child.IsCaredToday = false;
                child.IsPlayedToday = false;
            }
            else
            {
                child.AgeDays++;
                child.AgeYears = Math.Max(child.AgeYears, child.AgeDays / 7);
            }

            ChildLifeStage previousStage = child.Stage;
            child.Stage = this.GetStageForAgeYears(child.AgeYears);
            if (previousStage != child.Stage)
            {
                this.mod.NetSync.Broadcast(
                    MessageType.ChildGrewUp,
                    new ChildSyncMessage
                    {
                        Child = child
                    },
                    child.ParentAId,
                    child.ParentBId);
            }

            if (gainedYears > 0 || previousStage != child.Stage)
            {
                anyChanged = true;
            }
        }

        this.RebuildChildrenForActiveState();
        if (anyChanged)
        {
            this.mod.MarkDataDirty("Child feeding/growth day update processed.", flushNow: true);
            this.mod.NetSync.BroadcastSnapshotToAll();
        }
    }

    public void OnUpdateTickedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableChildGrowth || !Context.IsWorldReady || Game1.paused)
            return;

        if (Game1.CurrentEvent != null)
            return;

        // Rebuild NPC registry toutes les 2 secondes
        if (Game1.ticks % 120 == 0)
            this.RebuildChildrenForActiveState();

        // Vérifier la routine horaire toutes les ~5 secondes (évite les appels à chaque tick)
        if (Game1.ticks % 300 == 0)
            this.TickRoutineCheckHost();

        // Tick IA toutes les 60 ticks (1 seconde)
        if (Game1.ticks % 60 != 0)
            return;

        foreach (GameLocation location in Game1.locations)
        {
            if (location.currentEvent != null)
                continue;

            foreach (NPC npc in location.characters.Where(this.IsRuntimeChildNpc).ToList())
            {
                if (!npc.modData.TryGetValue(ChildNpcIdKey, out string? childId)
                    || !this.mod.HostSaveData.Children.TryGetValue(childId, out ChildRecord? child))
                    continue;

                this.EnsureV3Defaults(child);

                if (child.Stage == ChildLifeStage.Infant)
                    continue;

                this.TickChildAiHost(npc, child, location);
            }
        }
    }

    // -------------------------------------------------------------------------
    // IA : tick principal par enfant
    // -------------------------------------------------------------------------
    private void TickChildAiHost(NPC npc, ChildRecord child, GameLocation currentLocation)
    {
        // Si le NPC est en train de marcher vers sa cible, on le laisse finir
        if (npc.controller != null)
        {
            // Vérifier que le path n'est pas bloqué (controller sans path = path échoué)
            if (npc.controller.pathToEndPoint is { Count: > 0 })
                return;

            // Path vide = échec du pathfinding, on abandonne proprement
            npc.controller = null;
            npc.Halt();
            this.IncrementPathFail(child.ChildId);
        }

        // Pas encore l'heure d'agir
        if (!this.IsAiReady(child.ChildId))
            return;

        // Déterminer la location cible selon la routine
        string targetLocationName = this.GetRoutineLocationForChild(child);
        GameLocation? targetLocation = Game1.getLocationFromName(targetLocationName) ?? currentLocation;

        // --- Si l'enfant n'est pas dans la bonne location, le téléporter proprement ---
        if (!string.Equals(currentLocation.NameOrUniqueName, targetLocation.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
        {
            this.TeleportChildToLocation(npc, child, targetLocation);
            this.SetAiState(child.ChildId, ChildAiState.Wander, cooldownTicks: 120);
            return;
        }

        // --- Choisir l'état IA ---
        ChildAiState state = this.GetAiState(child.ChildId);

        switch (state)
        {
            case ChildAiState.Idle:
                this.AiIdle(npc, child, currentLocation);
                break;

            case ChildAiState.Wander:
                this.AiWander(npc, child, currentLocation);
                break;

            case ChildAiState.GoHome:
                this.AiGoHome(npc, child, currentLocation);
                break;
        }
    }

    // -------------------------------------------------------------------------
    // IA : comportement Idle (pause naturelle)
    // -------------------------------------------------------------------------
    private void AiIdle(NPC npc, ChildRecord child, GameLocation location)
    {
        // Pendant l'idle, faire une petite animation
        this.TryPlayIdleEmote(npc, child);

        // Après la pause, décider si on erre ou si on rentre
        ChildAiState next = Game1.random.NextDouble() < 0.75 ? ChildAiState.Wander : ChildAiState.GoHome;
        int idleTicks = Game1.random.Next(180, 480); // 3-8 secondes
        this.SetAiState(child.ChildId, next, idleTicks);
    }

    // -------------------------------------------------------------------------
    // IA : comportement Wander (errer vers une ancre)
    // -------------------------------------------------------------------------
    private void AiWander(NPC npc, ChildRecord child, GameLocation location)
    {
        // Choisir une ancre dans la location courante, différente de la précédente
        Vector2 anchor = this.PickAnchor(location, child.ChildId);
        Vector2 currentTile = npc.Tile;

        // Si on est déjà sur l'ancre, passer en Idle
        if (Vector2.Distance(currentTile, anchor) < 1.5f)
        {
            this.SetAiState(child.ChildId, ChildAiState.Idle, Game1.random.Next(120, 360));
            return;
        }

        // Trouver une tuile safe proche de l'ancre
        if (!this.TryFindNearestSafeTile(location, anchor, maxRadius: 4, out Vector2 safeTile, npc))
        {
            // Aucune tuile safe autour de l'ancre → on attend et on réessaie avec une autre ancre
            this.aiCurrentAnchorById.Remove(child.ChildId);
            this.SetAiState(child.ChildId, ChildAiState.Wander, Game1.random.Next(120, 240));
            return;
        }

        // Assigner le PathFindController
        bool ok = this.TryAssignChildPathController(npc, location, safeTile);
        if (ok)
        {
            this.aiCurrentAnchorById[child.ChildId] = anchor;
            this.aiPathFailCountById[child.ChildId] = 0;
            // Pas de SetAiState ici — on restera en Wander jusqu'à ce que le path finisse
            // Le prochain tick vérifiera si npc.controller est null (= arrivé)
            this.SetAiState(child.ChildId, ChildAiState.Idle, cooldownTicks: 60);
        }
        else
        {
            this.IncrementPathFail(child.ChildId);
            int failCount = this.aiPathFailCountById.GetValueOrDefault(child.ChildId, 0);

            if (failCount >= 4)
            {
                // Trop d'échecs → téléporter directement sur une ancre safe
                this.TeleportToSafeAnchor(npc, child, location);
                this.aiPathFailCountById[child.ChildId] = 0;
                this.SetAiState(child.ChildId, ChildAiState.Idle, 300);
            }
            else
            {
                // Réessayer avec un cooldown exponentiel
                int cooldown = 60 * (1 << Math.Min(failCount, 4)); // 60, 120, 240, 480 ticks
                this.SetAiState(child.ChildId, ChildAiState.Wander, cooldown);
            }
        }
    }

    // -------------------------------------------------------------------------
    // IA : comportement GoHome (retour à l'ancre principale)
    // -------------------------------------------------------------------------
    private void AiGoHome(NPC npc, ChildRecord child, GameLocation location)
    {
        // L'ancre "home" est la première ancre de la location
        Vector2 homeTile = this.GetHomeTileForLocation(location);

        if (Vector2.Distance(npc.Tile, homeTile) < 1.5f)
        {
            this.SetAiState(child.ChildId, ChildAiState.Idle, Game1.random.Next(300, 600));
            return;
        }

        if (!this.TryFindNearestSafeTile(location, homeTile, maxRadius: 4, out Vector2 safeTile, npc))
        {
            this.SetAiState(child.ChildId, ChildAiState.Wander, 180);
            return;
        }

        bool ok = this.TryAssignChildPathController(npc, location, safeTile);
        this.SetAiState(child.ChildId, ok ? ChildAiState.Idle : ChildAiState.Wander,
            ok ? 120 : Game1.random.Next(120, 300));
    }

    // -------------------------------------------------------------------------
    // IA : tick de vérification de routine (location change selon l'heure)
    // -------------------------------------------------------------------------
    private void TickRoutineCheckHost()
    {
        int time = Game1.timeOfDay;
        if (time == this.aiLastRoutineCheckTime)
            return;

        this.aiLastRoutineCheckTime = time;

        foreach (ChildRecord child in this.mod.HostSaveData.Children.Values)
        {
            this.EnsureV3Defaults(child);
            if (child.Stage == ChildLifeStage.Infant)
                continue;

            string targetLocation = this.GetRoutineLocationForChild(child);
            if (this.aiCurrentLocationById.TryGetValue(child.ChildId, out string? lastLocation)
                && string.Equals(lastLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
                continue;

            // La location cible a changé → forcer un GoHome dans la nouvelle location
            this.aiCurrentLocationById[child.ChildId] = targetLocation;
            this.SetAiState(child.ChildId, ChildAiState.GoHome, cooldownTicks: 0);

            this.mod.Monitor.Log(
                $"[PR.AI.Child] Routine change: child={child.ChildId} -> location={targetLocation} time={time}",
                LogLevel.Trace);
        }
    }

    // -------------------------------------------------------------------------
    // IA : helpers
    // -------------------------------------------------------------------------
    private string GetRoutineLocationForChild(ChildRecord child)
    {
        bool isRainy = Game1.isRaining || Game1.isSnowing;
        RoutineSlot[] routine = isRainy ? RainyRoutine : DailyRoutine;
        int time = Game1.timeOfDay;

        RoutineSlot slot = routine[0];
        foreach (RoutineSlot s in routine)
        {
            if (time >= s.TimeStart && time < s.TimeEnd)
            {
                slot = s;
                break;
            }
        }

        return child.Stage switch
        {
            ChildLifeStage.Teen => slot.LocationTeen,
            ChildLifeStage.Adult => slot.LocationAdult,
            _ => slot.LocationChild,
        };
    }

    private Vector2 PickAnchor(GameLocation location, string childId)
    {
        string locName = location.NameOrUniqueName;
        if (!AnchorsByLocation.TryGetValue(locName, out Vector2[]? anchors) || anchors.Length == 0)
        {
            // Location inconnue : ancre aléatoire dans la zone centrale
            int w = location.Map?.Layers[0]?.LayerWidth ?? 20;
            int h = location.Map?.Layers[0]?.LayerHeight ?? 20;
            return new Vector2(Game1.random.Next(3, Math.Max(4, w - 3)),
                               Game1.random.Next(3, Math.Max(4, h - 3)));
        }

        // Exclure l'ancre courante pour éviter de rester sur place
        Vector2 currentAnchor = this.aiCurrentAnchorById.GetValueOrDefault(childId, Vector2.Zero);
        Vector2[] candidates = anchors
            .Where(a => Vector2.Distance(a, currentAnchor) > 2f)
            .ToArray();

        if (candidates.Length == 0)
            candidates = anchors;

        return candidates[Game1.random.Next(candidates.Length)];
    }

    private Vector2 GetHomeTileForLocation(GameLocation location)
    {
        string locName = location.NameOrUniqueName;
        if (AnchorsByLocation.TryGetValue(locName, out Vector2[]? anchors) && anchors.Length > 0)
            return anchors[0];

        return new Vector2(
            (location.Map?.Layers[0]?.LayerWidth ?? 20) / 2f,
            (location.Map?.Layers[0]?.LayerHeight ?? 20) / 2f);
    }

    private void TeleportChildToLocation(NPC npc, ChildRecord child, GameLocation targetLocation)
    {
        GameLocation? currentLocation = npc.currentLocation;
        if (currentLocation != null
            && !string.Equals(currentLocation.NameOrUniqueName, targetLocation.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
        {
            currentLocation.characters.Remove(npc);
            targetLocation.addCharacter(npc);
            npc.currentLocation = targetLocation;
        }

        Vector2 homeTile = this.GetHomeTileForLocation(targetLocation);
        if (this.TryFindNearestSafeTile(targetLocation, homeTile, maxRadius: 8, out Vector2 safeTile, npc))
        {
            npc.setTileLocation(safeTile);
            npc.Position = safeTile * 64f;
        }

        npc.controller = null;
        npc.Halt();
        child.RoutineZone = targetLocation.NameOrUniqueName;
        this.aiCurrentAnchorById.Remove(child.ChildId);

        this.mod.Monitor.Log(
            $"[PR.AI.Child] Teleport child={child.ChildId} -> location={targetLocation.NameOrUniqueName}",
            LogLevel.Trace);
    }

    private void TeleportToSafeAnchor(NPC npc, ChildRecord child, GameLocation location)
    {
        string locName = location.NameOrUniqueName;
        if (!AnchorsByLocation.TryGetValue(locName, out Vector2[]? anchors) || anchors.Length == 0)
            return;

        foreach (Vector2 anchor in anchors.OrderBy(_ => Game1.random.Next()))
        {
            if (this.TryFindNearestSafeTile(location, anchor, maxRadius: 3, out Vector2 safe, npc))
            {
                npc.controller = null;
                npc.Halt();
                npc.setTileLocation(safe);
                npc.Position = safe * 64f;
                this.aiCurrentAnchorById[child.ChildId] = anchor;
                this.mod.Monitor.Log(
                    $"[PR.AI.Child] Emergency teleport child={child.ChildId} -> tile=({safe.X},{safe.Y})",
                    LogLevel.Trace);
                return;
            }
        }
    }

    private bool TryAssignChildPathController(NPC npc, GameLocation location, Vector2 targetTile)
    {
        Point targetPoint = new((int)Math.Floor(targetTile.X), (int)Math.Floor(targetTile.Y));
        try
        {
            npc.Halt();
            var controller = new StardewValley.Pathfinding.PathFindController(npc, location, targetPoint, -1);

            // Vérifier que le pathfinder a réellement trouvé un chemin
            if (controller.pathToEndPoint is null || controller.pathToEndPoint.Count == 0)
            {
                this.mod.Monitor.Log(
                    $"[PR.AI.Child] No path found for {npc.Name} -> ({targetPoint.X},{targetPoint.Y})",
                    LogLevel.Trace);
                return false;
            }

            npc.controller = controller;
            npc.movementPause = 0;
            return true;
        }
        catch (Exception ex)
        {
            this.mod.Monitor.Log(
                $"[PR.AI.Child] Path assign exception for {npc.Name}: {ex.Message}",
                LogLevel.Trace);
            return false;
        }
    }

    private void TryPlayIdleEmote(NPC npc, ChildRecord child)
    {
        // Emotes selon le stage : enfant joue, ado rêve, adulte inspecte
        int[] childEmotes = { 8, 12, 32 };  // happy, music, question
        int[] teenEmotes = { 4, 8, 40 };   // sad, happy, angry
        int[] adultEmotes = { 8, 16, 56 };  // happy, exclamation, working

        int[] pool = child.Stage switch
        {
            ChildLifeStage.Teen => teenEmotes,
            ChildLifeStage.Adult => adultEmotes,
            _ => childEmotes,
        };

        // Ne jouer un emote que 20% du temps pour ne pas spammer
        if (Game1.random.NextDouble() < 0.2)
        {
            try { npc.doEmote(pool[Game1.random.Next(pool.Length)]); }
            catch { /* ignore */ }
        }
    }

    // IA state machine helpers
    private bool IsAiReady(string childId)
    {
        return !this.aiNextActionTickById.TryGetValue(childId, out int next) || Game1.ticks >= next;
    }

    private ChildAiState GetAiState(string childId)
    {
        return this.aiStateByChildId.TryGetValue(childId, out ChildAiState s) ? s : ChildAiState.Wander;
    }

    private void SetAiState(string childId, ChildAiState state, int cooldownTicks = 60)
    {
        this.aiStateByChildId[childId] = state;
        this.aiNextActionTickById[childId] = Game1.ticks + Math.Max(0, cooldownTicks);
    }

    private void IncrementPathFail(string childId)
    {
        this.aiPathFailCountById[childId] =
            this.aiPathFailCountById.GetValueOrDefault(childId, 0) + 1;
    }

    public void BroadcastNpcSyncHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableChildGrowth || !Context.IsWorldReady)
        {
            return;
        }

        NpcSyncMessage sync = new()
        {
            Channel = "child",
            SequenceId = Game1.ticks
        };

        foreach (GameLocation location in Game1.locations)
        {
            foreach (NPC npc in location.characters.Where(this.IsRuntimeChildNpc))
            {
                if (!npc.modData.TryGetValue(ChildNpcIdKey, out string? childId)
                    || string.IsNullOrWhiteSpace(childId))
                {
                    continue;
                }

                sync.Entries.Add(new NpcSyncEntryMessage
                {
                    GroupId = childId,
                    LocationName = location.NameOrUniqueName,
                    NpcName = npc.Name,
                    PixelX = npc.Position.X,
                    PixelY = npc.Position.Y,
                    VelocityX = 0f,
                    VelocityY = 0f,
                    FacingDirection = npc.FacingDirection,
                    AnimationFrame = npc.Sprite?.currentFrame ?? 0

                });
            }
        }

        if (sync.Entries.Count > 0)
        {
            this.mod.NetSync.Broadcast(MessageType.NpcSync, sync);
        }
    }

    public void ApplyNpcSyncClient(NpcSyncMessage sync)
    {
        if (Game1.CurrentEvent != null) return;

        if (sync is null
            || this.mod.IsHostPlayer
            || !Context.IsWorldReady
            || !string.Equals(sync.Channel, "child", StringComparison.OrdinalIgnoreCase)
            || sync.Entries.Count == 0)
        {
            return;
        }

        foreach (NpcSyncEntryMessage entry in sync.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.GroupId)) continue;

            ChildRecord? child = this.mod.ClientSnapshot.Children.FirstOrDefault(p =>
                string.Equals(p.ChildId, entry.GroupId, StringComparison.OrdinalIgnoreCase));
            if (child is null) continue;

            this.EnsureV3Defaults(child);
            if (child.Stage == ChildLifeStage.Infant) continue;

            string seqKey = $"child:{entry.GroupId}";
            if (this.lastAppliedNpcSyncSequenceByChild.TryGetValue(seqKey, out long lastSeq)
                && sync.SequenceId <= lastSeq)
            {
                continue;
            }

            GameLocation? location = Game1.getLocationFromName(entry.LocationName);
            if (location is null) continue;

            NPC? npc = location.characters.FirstOrDefault(p =>
                this.IsRuntimeChildNpc(p)
                && p.modData.TryGetValue(ChildNpcIdKey, out string? existingId)
                && string.Equals(existingId, entry.GroupId, StringComparison.OrdinalIgnoreCase));

            if (npc is null)
            {
                this.TrySpawnOrRefreshRuntimeNpc(child);
                location = Game1.getLocationFromName(entry.LocationName);
                if (location is null) continue;

                npc = location.characters.FirstOrDefault(p =>
                    this.IsRuntimeChildNpc(p)
                    && p.modData.TryGetValue(ChildNpcIdKey, out string? existingId)
                    && string.Equals(existingId, entry.GroupId, StringComparison.OrdinalIgnoreCase));

                if (npc is null) continue;
            }

            // --- C'EST ICI QUE CA CHANGE ---

            Vector2 targetPos = new(entry.PixelX, entry.PixelY);
            float distance = Vector2.Distance(npc.Position, targetPos);

            if (distance > 128f)
                npc.Position = targetPos;
            else
                npc.Position = Vector2.Lerp(npc.Position, targetPos, 0.1f);

            // Appliquer direction ET frame directement — pas d'Animate()
            npc.FacingDirection = Math.Clamp(entry.FacingDirection, 0, 3);

            if (npc.Sprite != null && entry.AnimationFrame >= 0)
            {
                npc.Sprite.currentFrame = entry.AnimationFrame;
                npc.Sprite.UpdateSourceRect();
            }

            this.lastAppliedNpcSyncSequenceByChild[seqKey] = sync.SequenceId;
        }
    }

    public void OnRenderedWorldLocal(RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null)
        {
            return;
        }

        Point absolutePoint = new(Game1.getMouseX(true), Game1.getMouseY(true));
        if (!this.TryGetHoveredChild(absolutePoint, Game1.currentCursorTile, out NPC? npc, out ChildRecord? child))
        {
            return;
        }

        if (!this.CanActorInteractWithChild(child!, this.mod.LocalPlayerId))
        {
            return;
        }

        float bob = (float)Math.Sin(Game1.ticks / 10f) * 2f;
        Vector2 worldPos = npc!.Position + new Vector2(32f, -80f + bob);
        Vector2 localPos = Game1.GlobalToLocal(Game1.viewport, worldPos);
        e.SpriteBatch.Draw(Game1.mouseCursors, localPos, TalkIconSource, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
    }

    public bool TryHandleLocalInteractionButton(ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null)
        {
            return false;
        }

        Point absolutePoint = new((int)e.Cursor.AbsolutePixels.X, (int)e.Cursor.AbsolutePixels.Y);
        if (!this.TryGetHoveredChild(absolutePoint, e.Cursor.GrabTile, out NPC? npc, out ChildRecord? child))
        {
            return false;
        }

        if (!this.CanActorInteractWithChild(child!, this.mod.LocalPlayerId))
        {
            this.mod.Notifier.NotifyWarn("Vous n'êtes pas autorisé à interagir avec cet enfant.", "[PR.UI.ChildInteraction]");
            this.mod.Monitor.Log(
                $"[PR.UI.ChildInteraction] Local interaction denied for player {this.mod.LocalPlayerId} on child {child!.ChildId}.",
                LogLevel.Trace);
            return true;
        }

        if (e.Button == SButton.MouseRight)
        {
            Item? held = Game1.player.ActiveObject;
            if (!this.IsValidFoodItem(held))
            {
                return false;
            }

            bool ok = this.FeedChildFromLocal(child!.ChildId, held!.QualifiedItemId, out string feedMessage);
            if (ok)
            {
                this.mod.Notifier.NotifyInfo(feedMessage, "[PR.System.ChildGrowth]");
            }
            else
            {
                this.mod.Notifier.NotifyWarn(feedMessage, "[PR.System.ChildGrowth]");
            }

            return true;
        }

        if (e.Button != SButton.MouseLeft)
        {
            return false;
        }

        this.mod.Monitor.Log($"[PR.UI.ChildInteraction] Opening interaction menu for child {child!.ChildId}.", LogLevel.Trace);
        Game1.activeClickableMenu = new UI.ChildInteractionMenu(child.ChildId, child.ChildName, this.HandleChildInteractionActionLocal);
        return true;
    }

    public bool IsValidFoodItem(Item? item)
    {
        return item is SObject obj
               && item.Stack > 0
               && obj.QualifiedItemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
               && obj.Edibility > 0;
    }

    public bool OpenFeedInventoryMenuFromLocal(string childIdOrName, out string message)
    {
        if (string.IsNullOrWhiteSpace(childIdOrName))
        {
            message = "Child id/name is required.";
            return false;
        }

        if (!this.TryFindChildRecordForLocal(childIdOrName.Trim(), out ChildRecord? child))
        {
            message = $"Child '{childIdOrName}' not found.";
            return false;
        }

        if (!this.CanActorInteractWithChild(child!, this.mod.LocalPlayerId))
        {
            message = "Only a parent (or host) can interact with this child.";
            return false;
        }

        Game1.activeClickableMenu = new UI.ChildFeedInventoryMenu(this.mod, child!.ChildId, child.ChildName, this.HandleFeedItemPickedLocal);
        this.mod.Monitor.Log($"[PR.UI.ChildInteraction] Opened feed inventory menu for child {child.ChildId}.", LogLevel.Trace);
        message = "Feed inventory opened.";
        return true;
    }

    public void OnAssetRequested(AssetRequestedEventArgs e)
    {
        string assetName = e.NameWithoutLocale.BaseName;
        bool isPortrait = assetName.StartsWith("Portraits/PR_Child_", StringComparison.OrdinalIgnoreCase)
            || assetName.StartsWith("Portraits/PR_AdultChild_", StringComparison.OrdinalIgnoreCase);
        bool isCharacter = assetName.StartsWith("Characters/PR_Child_", StringComparison.OrdinalIgnoreCase)
            || assetName.StartsWith("Characters/PR_AdultChild_", StringComparison.OrdinalIgnoreCase);
        if (!isPortrait && !isCharacter)
        {
            return;
        }

        int slash = assetName.LastIndexOf('/');
        string runtimeName = slash >= 0 ? assetName[(slash + 1)..] : assetName;
        string templateName = this.ResolveTemplateFromRuntimeName(runtimeName);
        string fallbackTemplate = runtimeName.StartsWith("PR_AdultChild_", StringComparison.OrdinalIgnoreCase)
            ? "Abigail"
            : "Vincent";
        string prefix = isPortrait ? "Portraits" : "Characters";

        e.LoadFrom(
            () =>
            {
                try
                {
                    return Game1.content.Load<Texture2D>($"{prefix}\\{templateName}");
                }
                catch
                {
                    return Game1.content.Load<Texture2D>($"{prefix}\\{fallbackTemplate}");
                }
            },
            AssetLoadPriority.Exclusive);
    }

    public void RebuildAdultChildrenForActiveState()
    {
        this.RebuildChildrenForActiveState();
    }

    public void RebuildChildrenForActiveState()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableChildGrowth || !Context.IsWorldReady)
        {
            return;
        }

        foreach (ChildRecord child in this.mod.HostSaveData.Children.Values)
        {
            this.EnsureV3Defaults(child);
            this.TrySpawnOrRefreshRuntimeNpc(child);
        }
    }

    public IReadOnlyList<ChildPublicState> BuildPublicStatesSnapshot()
    {
        if (!this.mod.IsHostPlayer)
        {
            return this.mod.ClientSnapshot.ChildRuntimeStates;
        }

        List<ChildPublicState> states = new();
        foreach (ChildRecord child in this.mod.HostSaveData.Children.Values)
        {
            this.EnsureV3Defaults(child);
            states.Add(new ChildPublicState
            {
                ChildId = child.ChildId,
                ChildName = child.ChildName,
                AgeYears = child.AgeYears,
                Stage = child.Stage,
                IsFedToday = child.IsFedToday,
                FeedingProgress = child.FeedingProgress,
                AssignedTask = child.AssignedTask,
                AutoMode = child.AutoMode,
                IsWorkerEnabled = child.IsWorkerEnabled,
                RoutineZone = child.RoutineZone,
                RuntimeNpcName = child.RuntimeNpcName
            });
        }

        return states;
    }

    public bool DebugAgeChild(string token, int days, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can age children.";
            return false;
        }

        if (!this.TryFindChild(token, out ChildRecord? child) || child is null)
        {
            message = $"Child '{token}' not found.";
            return false;
        }

        ChildRecord foundChild = child;
        this.EnsureV3Defaults(foundChild);
        foundChild.AgeDays = Math.Max(0, foundChild.AgeDays + days);
        foundChild.AgeYears = Math.Max(0, foundChild.AgeDays / 7);
        foundChild.Stage = this.GetStageForAgeYears(foundChild.AgeYears);
        this.TrySpawnOrRefreshRuntimeNpc(foundChild);

        this.mod.MarkDataDirty($"Debug age changed for child {foundChild.ChildId}.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        message = $"{foundChild.ChildName} age is now {foundChild.AgeYears} years ({foundChild.Stage}).";
        return true;
    }

    public bool FeedChildFromLocal(string childIdOrName, string? requestedItemId, out string message)
    {
        return this.SendChildInteractionCommandFromLocal(ChildCommandAction.Feed, childIdOrName, requestedItemId, out message);
    }

    public bool CareChildFromLocal(string childIdOrName, out string message)
    {
        return this.SendChildInteractionCommandFromLocal(ChildCommandAction.Care, childIdOrName, null, out message);
    }

    public bool PlayWithChildFromLocal(string childIdOrName, out string message)
    {
        return this.SendChildInteractionCommandFromLocal(ChildCommandAction.Play, childIdOrName, null, out message);
    }

    public bool InteractChildFromLocal(string childIdOrName, string actionToken, out string message)
    {
        if (string.IsNullOrWhiteSpace(actionToken))
        {
            message = "Action is required (care|play|feed).";
            return false;
        }

        switch (actionToken.Trim().ToLowerInvariant())
        {
            case "care":
                return this.CareChildFromLocal(childIdOrName, out message);
            case "play":
                return this.PlayWithChildFromLocal(childIdOrName, out message);
            case "feed":
                return this.FeedChildFromLocal(childIdOrName, null, out message);
            default:
                message = "Action must be one of: care|play|feed.";
                return false;
        }
    }

    public bool SetChildAgeYearsFromLocal(string childIdOrName, int years, out string message)
    {
        if (string.IsNullOrWhiteSpace(childIdOrName))
        {
            message = "Child id/name is required.";
            return false;
        }

        if (this.mod.IsHostPlayer)
        {
            return this.SetChildAgeYearsHost(this.mod.LocalPlayerId, childIdOrName, years, out message);
        }

        ChildCommandMessage payload = new()
        {
            RequesterId = this.mod.LocalPlayerId,
            Action = ChildCommandAction.SetAgeYears,
            ChildIdOrName = childIdOrName.Trim(),
            AgeYears = years
        };
        this.mod.NetSync.SendToPlayer(MessageType.ChildCommand, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        message = "Age-set request sent to host.";
        return true;
    }

    public bool ForceGrowToAdultFromLocal(string childIdOrName, int targetYears, out string message)
    {
        int safeYears = Math.Max(16, targetYears);
        bool ok = this.SetChildAgeYearsFromLocal(childIdOrName, safeYears, out string setMsg);
        if (!ok)
        {
            message = setMsg;
            return false;
        }

        message = $"Adult growth forced to {safeYears}y. {setMsg}";
        return true;
    }

    public bool SetChildTaskFromLocal(string childIdOrName, string taskToken, out string message)
    {
        if (string.IsNullOrWhiteSpace(childIdOrName))
        {
            message = "Child id/name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(taskToken))
        {
            message = "Task token is required.";
            return false;
        }

        if (this.mod.IsHostPlayer)
        {
            return this.SetChildTaskHost(this.mod.LocalPlayerId, childIdOrName, taskToken, out message);
        }

        ChildCommandMessage payload = new()
        {
            RequesterId = this.mod.LocalPlayerId,
            Action = ChildCommandAction.SetTask,
            ChildIdOrName = childIdOrName.Trim(),
            TaskToken = taskToken.Trim()
        };
        this.mod.NetSync.SendToPlayer(MessageType.ChildCommand, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        message = "Task request sent to host.";
        return true;
    }

    public bool GetChildStatusFromLocal(string? childIdOrName, out string message)
    {
        IEnumerable<ChildRecord> source = this.mod.IsHostPlayer
            ? this.mod.HostSaveData.Children.Values
            : this.mod.ClientSnapshot.Children;
        List<ChildRecord> children = source.ToList();
        if (children.Count == 0)
        {
            message = "No children found.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(childIdOrName))
        {
            ChildRecord first = children[0];
            message = this.FormatChildStatus(first);
            return true;
        }

        ChildRecord? child = this.FindChildInCollection(children, childIdOrName!);
        if (child is null)
        {
            message = $"Child '{childIdOrName}' not found.";
            return false;
        }

        message = this.FormatChildStatus(child);
        return true;
    }

    public void HandleChildCommandHost(ChildCommandMessage command, long senderId)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        if (senderId != command.RequesterId)
        {
            this.mod.Monitor.Log(
                $"[PR.Net] Child command rejected: sender mismatch (sender={senderId}, requester={command.RequesterId}, action={command.Action}).",
                LogLevel.Warn);
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Child command rejected (sender mismatch).");
            return;
        }

        bool success;
        string message;
        switch (command.Action)
        {
            case ChildCommandAction.Feed:
                success = this.FeedChildHost(command.RequesterId, command.ChildIdOrName, command.ItemId, out message);
                break;
            case ChildCommandAction.SetAgeYears:
                success = this.SetChildAgeYearsHost(command.RequesterId, command.ChildIdOrName, command.AgeYears, out message);
                break;
            case ChildCommandAction.SetTask:
                success = this.SetChildTaskHost(command.RequesterId, command.ChildIdOrName, command.TaskToken, out message);
                break;
            case ChildCommandAction.Care:
                success = this.CareChildHost(command.RequesterId, command.ChildIdOrName, out message);
                break;
            case ChildCommandAction.Play:
                success = this.PlayChildHost(command.RequesterId, command.ChildIdOrName, out message);
                break;
            default:
                success = false;
                message = "Unsupported child command.";
                break;
        }

        if (!success)
        {
            this.mod.Monitor.Log(
                $"[PR.Net] Child command rejected for player {senderId}: {command.Action} -> {message}",
                LogLevel.Trace);
        }

        this.mod.NetSync.SendToPlayer(
            MessageType.ChildCommandResult,
            new ChildCommandResultMessage
            {
                Success = success,
                ChildId = command.ChildIdOrName,
                Message = message
            },
            senderId);
    }

    public bool FeedChildHost(long actorId, string childIdOrName, string? requestedItemId, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can process feeding.";
            return false;
        }

        if (!this.TryFindChild(childIdOrName, out ChildRecord? child) || child is null)
        {
            message = $"Child '{childIdOrName}' not found.";
            return false;
        }

        ChildRecord foundChild = child;
        this.EnsureV3Defaults(foundChild);
        if (foundChild.IsFedToday)
        {
            message = $"{foundChild.ChildName} is already fed today.";
            return false;
        }

        if (!this.IsParentOrHost(foundChild, actorId))
        {
            message = "Only a parent (or host) can feed this child.";
            return false;
        }

        Farmer? actor = this.mod.FindFarmerById(actorId, includeOffline: false);
        if (actor is null)
        {
            message = "Player must be online to feed child.";
            return false;
        }

        if (!TryConsumeItem(actor, requestedItemId, this.IsValidFoodItem, out string consumedName, out string failureReason))
        {
            message = failureReason;
            return false;
        }

        foundChild.IsFedToday = true;
        foundChild.FeedingProgress++;
        foundChild.LastProcessedDay = Math.Min(foundChild.LastProcessedDay, this.mod.GetCurrentDayNumber() - 1);
        this.mod.MarkDataDirty($"Child fed ({foundChild.ChildId}) by {actorId}.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        this.mod.Monitor.Log($"[PR.System.ChildGrowth] Child {foundChild.ChildId} fed by {actorId} using {consumedName}.", LogLevel.Info);
        message = $"{foundChild.ChildName} was fed using {consumedName}.";
        return true;
    }

    public bool CareChildHost(long actorId, string childIdOrName, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can process child interactions.";
            return false;
        }

        if (!this.TryFindChild(childIdOrName, out ChildRecord? child) || child is null)
        {
            message = $"Child '{childIdOrName}' not found.";
            return false;
        }

        ChildRecord foundChild = child;
        if (!this.CanActorInteractWithChild(foundChild, actorId))
        {
            message = "Only a parent (or host) can interact with this child.";
            return false;
        }

        Farmer? actor = this.mod.FindFarmerById(actorId, includeOffline: false);
        if (actor is null)
        {
            message = "Player must be online to interact with child.";
            return false;
        }

        if (foundChild.IsCaredToday)
        {
            message = "Vous vous êtes déjà occupé de votre enfant aujourd'hui.";
            return false;
        }

        foundChild.IsCaredToday = true;
        this.mod.MarkDataDirty($"Child care interaction ({foundChild.ChildId}) by {actorId}.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();

        this.mod.Monitor.Log($"[PR.System.ChildGrowth] Care interaction accepted for child {foundChild.ChildId} by {actorId}.", LogLevel.Trace);
        message = "Vous vous occupez de votre enfant.";
        return true;
    }

    public bool PlayChildHost(long actorId, string childIdOrName, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can process child interactions.";
            return false;
        }

        if (!this.TryFindChild(childIdOrName, out ChildRecord? child) || child is null)
        {
            message = $"Child '{childIdOrName}' not found.";
            return false;
        }

        ChildRecord foundChild = child;
        if (!this.CanActorInteractWithChild(foundChild, actorId))
        {
            message = "Only a parent (or host) can interact with this child.";
            return false;
        }

        Farmer? actor = this.mod.FindFarmerById(actorId, includeOffline: false);
        if (actor is null)
        {
            message = "Player must be online to interact with child.";
            return false;
        }

        if (foundChild.IsPlayedToday)
        {
            message = "Vous avez déjà joué avec votre enfant aujourd'hui.";
            return false;
        }

        foundChild.IsPlayedToday = true;
        this.mod.MarkDataDirty($"Child play interaction ({foundChild.ChildId}) by {actorId}.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();

        this.mod.Monitor.Log($"[PR.System.ChildGrowth] Play interaction accepted for child {foundChild.ChildId} by {actorId}.", LogLevel.Trace);
        message = "Vous jouez avec votre enfant.";
        return true;
    }

    public bool SetChildAgeYearsHost(long actorId, string childIdOrName, int years, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can set child age.";
            return false;
        }

        if (!this.mod.IsHostPlayer || actorId != this.mod.LocalPlayerId)
        {
            message = "Only host can use child age.set.";
            return false;
        }

        if (!this.TryFindChild(childIdOrName, out ChildRecord? child) || child is null)
        {
            message = $"Child '{childIdOrName}' not found.";
            return false;
        }

        this.EnsureV3Defaults(child);
        ChildLifeStage previousStage = child.Stage;
        child.AgeYears = Math.Max(0, years);
        child.AgeDays = Math.Max(child.AgeDays, child.AgeYears * 7);
        child.Stage = this.GetStageForAgeYears(child.AgeYears);
        if (child.Stage == ChildLifeStage.Adult && string.IsNullOrWhiteSpace(child.AdultNpcName))
        {
            child.AdultNpcName = child.RuntimeNpcName;
        }

        this.TrySpawnOrRefreshRuntimeNpc(child);

        this.mod.MarkDataDirty($"Child age years force-set ({child.ChildId}).", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        this.mod.Monitor.Log(
            $"[PR.System.ChildGrowth] Force age set child={child.ChildId} name={child.ChildName} stage {previousStage} -> {child.Stage}, years={child.AgeYears}.",
            LogLevel.Info);
        message = $"{child.ChildName} age set to {child.AgeYears} years ({child.Stage}).";
        return true;
    }

    public bool SetChildTaskHost(long actorId, string childIdOrName, string taskToken, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can assign child tasks.";
            return false;
        }

        if (!this.TryFindChild(childIdOrName, out ChildRecord? child) || child is null)
        {
            message = $"Child '{childIdOrName}' not found.";
            return false;
        }

        ChildRecord foundChild = child;
        this.EnsureV3Defaults(foundChild);
        if (!this.IsParentOrHost(foundChild, actorId))
        {
            message = "Only a parent (or host) can assign child task.";
            return false;
        }

        if (!TryParseTaskToken(taskToken, out ChildTaskType parsed))
        {
            message = "Task must be one of: auto|water|feed|collect|harvest|ship|fish|stop.";
            return false;
        }

        if (parsed == ChildTaskType.Fish && !this.mod.Config.EnableChildFishingTask)
        {
            message = "Child fishing task is disabled by config.";
            return false;
        }

        foundChild.AssignedTask = parsed;
        foundChild.AutoMode = parsed == ChildTaskType.Auto;
        foundChild.IsWorkerEnabled = parsed != ChildTaskType.Stop;
        this.mod.MarkDataDirty($"Child task updated ({foundChild.ChildId} -> {parsed}).", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        message = $"{foundChild.ChildName} task updated: {parsed} (auto={foundChild.AutoMode}).";
        return true;
    }

    private bool SendChildInteractionCommandFromLocal(ChildCommandAction action, string childIdOrName, string? itemId, out string message)
    {
        if (string.IsNullOrWhiteSpace(childIdOrName))
        {
            message = "Child id/name is required.";
            return false;
        }

        string childToken = childIdOrName.Trim();
        if (this.mod.IsHostPlayer)
        {
            return action switch
            {
                ChildCommandAction.Feed => this.FeedChildHost(this.mod.LocalPlayerId, childToken, itemId, out message),
                ChildCommandAction.Care => this.CareChildHost(this.mod.LocalPlayerId, childToken, out message),
                ChildCommandAction.Play => this.PlayChildHost(this.mod.LocalPlayerId, childToken, out message),
                _ => throw new InvalidOperationException($"Unsupported local child interaction action {action}.")
            };
        }

        ChildCommandMessage payload = new()
        {
            RequesterId = this.mod.LocalPlayerId,
            Action = action,
            ChildIdOrName = childToken,
            ItemId = itemId?.Trim() ?? string.Empty
        };
        this.mod.NetSync.SendToPlayer(MessageType.ChildCommand, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        message = action switch
        {
            ChildCommandAction.Feed => "Feed request sent to host.",
            ChildCommandAction.Care => "Care request sent to host.",
            ChildCommandAction.Play => "Play request sent to host.",
            _ => "Child interaction request sent to host."
        };
        return true;
    }

    private void HandleChildInteractionActionLocal(string childId, UI.ChildInteractionAction action)
    {
        switch (action)
        {
            case UI.ChildInteractionAction.Care:
                {
                    bool ok = this.CareChildFromLocal(childId, out string msg);
                    if (ok)
                    {
                        this.mod.Notifier.NotifyInfo(msg, "[PR.System.ChildGrowth]");
                    }
                    else
                    {
                        this.mod.Notifier.NotifyWarn(msg, "[PR.System.ChildGrowth]");
                    }

                    break;
                }
            case UI.ChildInteractionAction.Play:
                {
                    bool ok = this.PlayWithChildFromLocal(childId, out string msg);
                    if (ok)
                    {
                        this.mod.Notifier.NotifyInfo(msg, "[PR.System.ChildGrowth]");
                    }
                    else
                    {
                        this.mod.Notifier.NotifyWarn(msg, "[PR.System.ChildGrowth]");
                    }

                    break;
                }
            default:
                break;
        }
    }

    private void HandleFeedItemPickedLocal(string childId, string? qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            this.mod.Notifier.NotifyInfo("Action annulée.", "[PR.UI.ChildInteraction]");
            return;
        }

        bool ok = this.FeedChildFromLocal(childId, qualifiedItemId, out string msg);
        if (ok)
        {
            this.mod.Notifier.NotifyInfo(msg, "[PR.System.ChildGrowth]");
        }
        else
        {
            this.mod.Notifier.NotifyWarn(msg, "[PR.System.ChildGrowth]");
        }
    }

    private bool TryFindChildRecordForLocal(string childIdOrName, out ChildRecord? child)
    {
        if (this.mod.IsHostPlayer)
        {
            return this.TryFindChild(childIdOrName, out child);
        }

        child = this.FindChildInCollection(this.mod.ClientSnapshot.Children, childIdOrName);
        return child is not null;
    }

    private bool TryGetHoveredChild(Point absolutePoint, Vector2 cursorTile, out NPC? childNpc, out ChildRecord? child)
    {
        childNpc = null;
        child = null;
        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            return false;
        }

        float bestDistance = float.MaxValue;
        foreach (NPC npc in Game1.currentLocation.characters)
        {
            if (!this.IsRuntimeChildNpc(npc))
            {
                continue;
            }

            if (!npc.modData.TryGetValue(ChildNpcIdKey, out string? childId) || string.IsNullOrWhiteSpace(childId))
            {
                continue;
            }

            if (!this.TryFindChildRecordForLocal(childId, out ChildRecord? candidateChild))
            {
                continue;
            }

            Microsoft.Xna.Framework.Rectangle box = npc.GetBoundingBox();
            box.Inflate(10, 10);
            bool hitBox = box.Contains(absolutePoint);
            float tileDistance = Vector2.Distance(cursorTile, npc.Tile);
            bool hitTile = tileDistance <= 1.35f;
            if (!hitBox && !hitTile)
            {
                continue;
            }

            if (tileDistance >= bestDistance)
            {
                continue;
            }

            bestDistance = tileDistance;
            childNpc = npc;
            child = candidateChild;
        }

        return childNpc is not null && child is not null;
    }

    private void EnsureV3Defaults(ChildRecord child)
    {
        if (child.AgeYears <= 0 && child.AgeDays > 0)
        {
            child.AgeYears = Math.Max(0, child.AgeDays / 7);
        }

        if (child.VisualProfile is null || child.VisualProfile.MixSeed == 0)
        {
            child.VisualProfile = this.GenerateVisualProfile(child);
        }

        child.Stage = this.GetStageForAgeYears(child.AgeYears);
        child.AssignedTask = child.AssignedTask is < ChildTaskType.Auto or > ChildTaskType.Stop
            ? ChildTaskType.Auto
            : child.AssignedTask;
        if (string.IsNullOrWhiteSpace(child.RoutineZone))
        {
            child.RoutineZone = "FarmHouse";
        }

        if (string.IsNullOrWhiteSpace(child.RuntimeNpcName))
        {
            child.RuntimeNpcName = $"PR_Child_{child.ChildId[..Math.Min(8, child.ChildId.Length)]}";
        }

        if (string.IsNullOrWhiteSpace(child.AdultNpcName))
        {
            child.AdultNpcName = child.RuntimeNpcName;
        }
    }

    private ChildLifeStage GetStageForAgeYears(int ageYears)
    {
        if (ageYears <= BabyMaxAgeYears)
        {
            return ChildLifeStage.Infant;
        }

        if (ageYears <= ChildMaxAgeYears)
        {
            return ChildLifeStage.Child;
        }

        if (ageYears <= TeenMaxAgeYears)
        {
            return ChildLifeStage.Teen;
        }

        return ChildLifeStage.Adult;
    }

    private int GetDailyGrowthYears(ChildRecord child, int day)
    {
        int min = Math.Max(0, this.mod.Config.ChildYearsPerFedDayMin);
        int max = Math.Max(min, this.mod.Config.ChildYearsPerFedDayMax);
        if (max <= min)
        {
            return min;
        }

        int seed = HashCode.Combine(child.ChildId, child.ParentAId, child.ParentBId, day);
        int span = max - min + 1;
        return min + Math.Abs(seed % span);
    }

    private ChildVisualProfile GenerateVisualProfile(ChildRecord child)
    {
        try
        {
            int seed = Math.Abs(HashCode.Combine(child.ParentAId, child.ParentBId, child.ChildId));
            string[] infantTemplates = { "Vincent", "Jas", "Leo" };
            string[] childTemplates = { "Jas", "Vincent", "Leo" };
            string[] teenTemplates = { "Sam", "Abigail", "Sebastian", "Penny" };
            string[] adultTemplates = { "Abigail", "Sam", "Leah", "Penny" };
            return new ChildVisualProfile
            {
                MixSeed = seed,
                SkinToneHex = PickColorHex(seed, 0),
                HairColorHex = PickColorHex(seed, 1),
                OutfitColorHex = PickColorHex(seed, 2),
                InfantTemplateNpc = infantTemplates[seed % infantTemplates.Length],
                ChildTemplateNpc = childTemplates[(seed / 3) % childTemplates.Length],
                TeenTemplateNpc = teenTemplates[(seed / 7) % teenTemplates.Length],
                AdultTemplateNpc = adultTemplates[(seed / 11) % adultTemplates.Length],
                IsFallback = false
            };
        }
        catch
        {
            return new ChildVisualProfile
            {
                MixSeed = 1,
                InfantTemplateNpc = "Vincent",
                ChildTemplateNpc = "Jas",
                TeenTemplateNpc = "Sam",
                AdultTemplateNpc = "Abigail",
                IsFallback = true
            };
        }
    }

    private static string PickColorHex(int seed, int salt)
    {
        int mixed = HashCode.Combine(seed, salt);
        byte r = (byte)(70 + Math.Abs(mixed % 140));
        byte g = (byte)(70 + Math.Abs((mixed / 7) % 140));
        byte b = (byte)(70 + Math.Abs((mixed / 13) % 140));
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private void TrySpawnOrRefreshRuntimeNpc(ChildRecord child)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        string targetLocationName = this.ResolveRoutineLocation(child);
        GameLocation? location = Game1.getLocationFromName(targetLocationName) ?? Game1.getFarm();
        if (location is null)
        {
            return;
        }

        string npcName = child.Stage == ChildLifeStage.Adult && !string.IsNullOrWhiteSpace(child.AdultNpcName)
            ? child.AdultNpcName
            : child.RuntimeNpcName;

        NPC? existing = this.FindRuntimeChildNpcAnyLocation(child.ChildId);
        if (existing is not null)
        {
            GameLocation? existingLocation = existing.currentLocation;
            if (existingLocation is not null
                && !string.Equals(existingLocation.NameOrUniqueName, location.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
            {
                existingLocation.characters.Remove(existing);
                location.addCharacter(existing);
                existing.currentLocation = location;
            }

            existing.Name = npcName;
            TagRuntimeNpc(existing, child);
            if (!this.IsSafeNpcPosition(location, existing.Position))
            {
                // ✅ Ignorer le NPC lui-même dans le check de collision
                Vector2 currentTile = existing.Tile;
                if (!this.IsSafeNpcTile(location, currentTile, ignoreNpc: existing))
                {
                    Vector2 fallbackTile = this.GetSpawnTileForChild(location, child);
                    if (this.TryFindNearestSafeTile(location, fallbackTile, maxRadius: 8, out Vector2 safeTile, ignoreNpc: existing))
                    {
                        existing.Position = safeTile * 64f;
                    }
                }
            }

            child.RuntimeNpcSpawned = true;
            child.RoutineZone = location.NameOrUniqueName;
            this.runtimeNpcByChildId[child.ChildId] = npcName;
            this.PruneChildNpcsToLocation(child, location.NameOrUniqueName);
            return;
        }

        string templateName = this.ResolveTemplateNpcName(child);
        Vector2 spawnTile = this.GetSpawnTileForChild(location, child);
        if (!this.TryFindNearestSafeTile(location, spawnTile, maxRadius: 8, out spawnTile))
        {
            spawnTile = this.ClampTileToLocation(location, spawnTile);
        }
        try
        {
            AnimatedSprite sprite = new($"Characters\\{templateName}", 0, 16, 32);
            Texture2D portrait = Game1.content.Load<Texture2D>($"Portraits\\{templateName}");
            NPC npc = new(
                sprite,
                spawnTile * 64f,
                location.NameOrUniqueName,
                2,
                npcName,
                false,
                portrait);
            TagRuntimeNpc(npc, child);
            location.addCharacter(npc);
            child.RuntimeNpcSpawned = true;
            child.RoutineZone = location.NameOrUniqueName;
            this.runtimeNpcByChildId[child.ChildId] = npc.Name;
            this.PruneChildNpcsToLocation(child, location.NameOrUniqueName);
        }
        catch (Exception ex)
        {
            this.mod.Monitor.Log(
                $"[PR.System.ChildGrowth] Spawn fallback for child '{child.ChildId}' ({templateName}) failed: {ex.Message}",
                StardewModdingAPI.LogLevel.Warn);
            TrySpawnFallback(location, child, spawnTile);
        }
    }

    private static void TrySpawnFallback(GameLocation location, ChildRecord child, Vector2 spawnTile)
    {
        try
        {
            AnimatedSprite sprite = new("Characters\\Vincent", 0, 16, 32);
            Texture2D portrait = Game1.content.Load<Texture2D>("Portraits\\Vincent");
            NPC npc = new(
                sprite,
                spawnTile * 64f,
                location.NameOrUniqueName,
                2,
                child.RuntimeNpcName,
                false,
                portrait);
            npc.modData[ChildNpcFlagKey] = "1";
            npc.modData[ChildNpcIdKey] = child.ChildId;
            npc.modData[ChildNpcStageKey] = child.Stage.ToString();
            location.addCharacter(npc);
            child.RuntimeNpcSpawned = true;
            child.VisualProfile.IsFallback = true;
            child.RoutineZone = location.NameOrUniqueName;
        }
        catch
        {
        }
    }

    private static void TagRuntimeNpc(NPC npc, ChildRecord child)
    {
        npc.modData[ChildNpcFlagKey] = "1";
        npc.modData[ChildNpcIdKey] = child.ChildId;
        npc.modData[ChildNpcStageKey] = child.Stage.ToString();
        npc.modData["PlayerRomance/ChildDisplayName"] = child.ChildName;
        npc.willDestroyObjectsUnderfoot = false;
    }

    private string ResolveRoutineLocation(ChildRecord child)
    {
        // Délègue au système de routines horaires
        return this.GetRoutineLocationForChild(child);
    }

    private string ResolveTemplateNpcName(ChildRecord child)
    {
        ChildVisualProfile profile = child.VisualProfile ?? new ChildVisualProfile();
        return child.Stage switch
        {
            ChildLifeStage.Infant => profile.InfantTemplateNpc,
            ChildLifeStage.Child => profile.ChildTemplateNpc,
            ChildLifeStage.Teen => profile.TeenTemplateNpc,
            _ => profile.AdultTemplateNpc
        };
    }

    private Vector2 GetSpawnTileForChild(GameLocation location, ChildRecord child)
    {
        // Utilise la première ancre de la location comme point de spawn
        return this.GetHomeTileForLocation(location);
    }

    private Vector2 ClampTileToLocation(GameLocation location, Vector2 tile)
    {
        int width = location.Map?.Layers[0]?.LayerWidth ?? 100;
        int height = location.Map?.Layers[0]?.LayerHeight ?? 100;
        float x = Math.Clamp(tile.X, 1f, Math.Max(1f, width - 2f));
        float y = Math.Clamp(tile.Y, 1f, Math.Max(1f, height - 2f));
        return new Vector2(x, y);
    }

    private void RemoveAllRuntimeChildNpcs()
    {
        foreach (GameLocation location in Game1.locations)
        {
            for (int i = location.characters.Count - 1; i >= 0; i--)
            {
                NPC npc = location.characters[i];
                if (this.IsRuntimeChildNpc(npc) || IsLegacyRuntimeChildName(npc.Name))
                {
                    location.characters.RemoveAt(i);
                }
            }
        }
    }

    private void PruneChildNpcsToLocation(ChildRecord child, string keepLocationName)
    {
        foreach (GameLocation location in Game1.locations)
        {
            for (int i = location.characters.Count - 1; i >= 0; i--)
            {
                NPC npc = location.characters[i];
                bool isLegacyNamed = string.Equals(npc.Name, child.RuntimeNpcName, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(npc.Name, child.AdultNpcName, StringComparison.OrdinalIgnoreCase);
                if (!this.IsRuntimeChildNpc(npc) && !isLegacyNamed)
                {
                    continue;
                }

                if (npc.modData.TryGetValue(ChildNpcIdKey, out string? existingId)
                    && string.Equals(existingId, child.ChildId, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(location.NameOrUniqueName, keepLocationName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    location.characters.RemoveAt(i);
                    continue;
                }

                if (isLegacyNamed)
                {
                    location.characters.RemoveAt(i);
                }
            }
        }
    }

    private bool IsRuntimeChildNpc(NPC npc)
    {
        return npc.modData.ContainsKey(ChildNpcFlagKey);
    }

    private bool IsSafeNpcPosition(GameLocation location, Vector2 worldPosition, NPC? ignoreNpc = null)
    {
        if (float.IsNaN(worldPosition.X)
            || float.IsNaN(worldPosition.Y)
            || float.IsInfinity(worldPosition.X)
            || float.IsInfinity(worldPosition.Y))
        {
            return false;
        }

        Vector2 tile = worldPosition / 64f;
        return this.IsSafeNpcTile(location, new Vector2((float)Math.Floor(tile.X), (float)Math.Floor(tile.Y)), ignoreNpc);
    }

    private bool IsSafeNpcTile(GameLocation location, Vector2 tile, NPC? ignoreNpc = null)
    {
        if (float.IsNaN(tile.X)
            || float.IsNaN(tile.Y)
            || float.IsInfinity(tile.X)
            || float.IsInfinity(tile.Y))
        {
            return false;
        }

        int width = location.Map?.Layers[0]?.LayerWidth ?? 0;
        int height = location.Map?.Layers[0]?.LayerHeight ?? 0;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (tile.X < 1f
            || tile.Y < 1f
            || tile.X >= width - 1f
            || tile.Y >= height - 1f)
        {
            return false;
        }

        int x = (int)Math.Floor(tile.X);
        int y = (int)Math.Floor(tile.Y);
        if (location.isWaterTile(x, y)
            || location.doesTileHaveProperty(x, y, "Water", "Back") is not null
            || location.Objects.ContainsKey(new Vector2(x, y))
            || location.doesTileHaveProperty(x, y, "NoPath", "Back") is not null)
        {
            return false;
        }

        if (location.characters.Any(p => p != ignoreNpc && Vector2.Distance(p.Tile, new Vector2(x, y)) < 0.1f))
        {
            return false;
        }

        Location tileLoc = new((int)Math.Floor(tile.X), (int)Math.Floor(tile.Y));
        if (location.isTileLocationOpen(tileLoc))
        {
            return true;
        }

        try
        {
            return location.isTilePassable(tileLoc, Game1.viewport);
        }
        catch
        {
            return false;
        }
    }

    private bool TryFindNearestSafeTile(GameLocation location, Vector2 desiredTile, int maxRadius, out Vector2 safeTile, NPC? ignoreNpc = null)
    {
        Vector2 clamped = this.ClampTileToLocation(location, desiredTile);
        if (this.IsSafeNpcTile(location, clamped, ignoreNpc))
        {
            safeTile = clamped;
            return true;
        }

        int centerX = (int)Math.Floor(clamped.X);
        int centerY = (int)Math.Floor(clamped.Y);
        int radius = Math.Max(1, maxRadius);
        for (int r = 1; r <= radius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > r)
                    {
                        continue;
                    }

                    Vector2 candidate = this.ClampTileToLocation(location, new Vector2(centerX + dx, centerY + dy));
                    if (this.IsSafeNpcTile(location, candidate, ignoreNpc))
                    {
                        safeTile = candidate;
                        return true;
                    }
                }
            }
        }

        safeTile = clamped;
        return false;
    }

    private string ResolveTemplateFromRuntimeName(string runtimeName)
    {
        IEnumerable<ChildRecord> children = this.mod.IsHostPlayer
            ? this.mod.HostSaveData.Children.Values
            : this.mod.ClientSnapshot.Children;
        ChildRecord? child = children.FirstOrDefault(p =>
            p.RuntimeNpcName.Equals(runtimeName, StringComparison.OrdinalIgnoreCase)
            || p.AdultNpcName.Equals(runtimeName, StringComparison.OrdinalIgnoreCase));
        if (child is not null)
        {
            return this.ResolveTemplateNpcName(child);
        }

        return runtimeName.StartsWith("PR_AdultChild_", StringComparison.OrdinalIgnoreCase)
            ? "Abigail"
            : "Vincent";
    }

    private static bool IsLegacyRuntimeChildName(string name)
    {
        return name.StartsWith("PR_Child_", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("PR_AdultChild_", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryFindChild(string token, out ChildRecord? child)
    {
        child = this.FindChildInCollection(this.mod.HostSaveData.Children.Values, token);
        return child is not null;
    }

    private NPC? FindRuntimeChildNpcAnyLocation(string childId)
    {
        foreach (GameLocation location in Game1.locations)
        {
            NPC? found = location.characters.FirstOrDefault(p =>
                this.IsRuntimeChildNpc(p)
                && p.modData.TryGetValue(ChildNpcIdKey, out string? existingId)
                && string.Equals(existingId, childId, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private ChildRecord? FindChildInCollection(IEnumerable<ChildRecord> children, string token)
    {
        string raw = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string normalized = NormalizeChildToken(raw);

        ChildRecord? exact = children.FirstOrDefault(c =>
            c.ChildId.Equals(raw, StringComparison.OrdinalIgnoreCase)
            || c.ChildName.Equals(raw, StringComparison.OrdinalIgnoreCase)
            || c.RuntimeNpcName.Equals(raw, StringComparison.OrdinalIgnoreCase)
            || c.AdultNpcName.Equals(raw, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        List<ChildRecord> fuzzy = children
            .Where(c =>
                NormalizeChildToken(c.ChildId).Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || NormalizeChildToken(c.ChildName).Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || NormalizeChildToken(c.RuntimeNpcName).Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || NormalizeChildToken(c.AdultNpcName).Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.ChildId.Length)
            .ThenBy(c => c.ChildName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return fuzzy.FirstOrDefault();
    }

    private static string NormalizeChildToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[value.Length];
        int written = 0;
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[written++] = char.ToLowerInvariant(ch);
            }
        }

        return written <= 0 ? string.Empty : new string(buffer[..written]);
    }

    private bool IsParentOrHost(ChildRecord child, long actorId)
    {
        return this.CanActorInteractWithChild(child, actorId);
    }

    private bool CanActorInteractWithChild(ChildRecord child, long actorId)
    {
        if (!Context.IsWorldReady)
        {
            return actorId == child.ParentAId || actorId == child.ParentBId;
        }

        long hostId = Game1.MasterPlayer?.UniqueMultiplayerID ?? this.mod.LocalPlayerId;
        return actorId == hostId || actorId == child.ParentAId || actorId == child.ParentBId;
    }

    private static bool TryConsumeItem(Farmer actor, string? requestedItemId, Func<Item?, bool> isValidFoodItem, out string consumedName, out string failureReason)
    {
        consumedName = string.Empty;
        failureReason = string.Empty;
        string requested = requestedItemId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(requested))
        {
            for (int i = 0; i < actor.Items.Count; i++)
            {
                Item? candidate = actor.Items[i];
                if (!isValidFoodItem(candidate))
                {
                    continue;
                }

                consumedName = candidate!.DisplayName;
                candidate.Stack--;
                if (candidate.Stack <= 0)
                {
                    actor.Items[i] = null;
                }

                return true;
            }

            failureReason = "Inventaire vide ou aucune nourriture valide à donner.";
            return false;
        }

        for (int i = 0; i < actor.Items.Count; i++)
        {
            Item? item = actor.Items[i];
            if (item is null || item.Stack <= 0)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(requested)
                && !string.Equals(item.QualifiedItemId, requested, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.ItemId, requested, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!isValidFoodItem(item))
            {
                failureReason = $"L'objet '{item.DisplayName}' n'est pas une nourriture valide.";
                return false;
            }

            consumedName = item.DisplayName;
            item.Stack--;
            if (item.Stack <= 0)
            {
                actor.Items[i] = null;
            }

            return true;
        }

        failureReason = $"Item '{requested}' introuvable dans l'inventaire.";
        return false;
    }

    private string FormatChildStatus(ChildRecord child)
    {
        return $"Child id={child.ChildId} name={child.ChildName} age={child.AgeYears}y stage={child.Stage} fedToday={child.IsFedToday} feedProgress={child.FeedingProgress} task={child.AssignedTask} auto={child.AutoMode} worker={child.IsWorkerEnabled} zone={child.RoutineZone}.";
    }

    private static bool TryParseTaskToken(string token, out ChildTaskType task)
    {
        switch (token.Trim().ToLowerInvariant())
        {
            case "auto":
                task = ChildTaskType.Auto;
                return true;
            case "water":
                task = ChildTaskType.Water;
                return true;
            case "feed":
                task = ChildTaskType.FeedAnimals;
                return true;
            case "collect":
                task = ChildTaskType.Collect;
                return true;
            case "harvest":
                task = ChildTaskType.Harvest;
                return true;
            case "ship":
                task = ChildTaskType.Ship;
                return true;
            case "fish":
                task = ChildTaskType.Fish;
                return true;
            case "stop":
            case "rest":
                task = ChildTaskType.Stop;
                return true;
            default:
                task = ChildTaskType.Auto;
                return false;
        }
    }

}
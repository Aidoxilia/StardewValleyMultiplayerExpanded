using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.Systems;

public sealed class PlayerProfileSystem
{
    private static readonly HashSet<string> ValidSeasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "spring",
        "summer",
        "fall",
        "winter"
    };

    private readonly ModEntry mod;

    public PlayerProfileSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public bool RequestBirthdayUpdateFromLocal(string season, int day, out string message)
    {
        string normalizedSeason = NormalizeSeason(season);
        if (!IsValidBirthday(normalizedSeason, day, out message))
        {
            return false;
        }

        PlayerProfileRecord current = this.GetOrCreateLocalSeedProfile();
        PlayerProfileUpdateRequestMessage request = new()
        {
            FromPlayerId = this.mod.LocalPlayerId,
            BirthdaySeason = normalizedSeason,
            BirthdayDay = day,
            FavoriteGiftItemIds = new List<string>(current.FavoriteGiftItemIds)
        };

        return this.SendOrApplyRequest(request, out message);
    }

    public bool RequestFavoritesUpdateFromLocal(IEnumerable<string> rawIds, out string message)
    {
        if (!TryNormalizeFavoriteIds(rawIds, out List<string> normalized, out message))
        {
            return false;
        }

        PlayerProfileRecord current = this.GetOrCreateLocalSeedProfile();
        PlayerProfileUpdateRequestMessage request = new()
        {
            FromPlayerId = this.mod.LocalPlayerId,
            BirthdaySeason = current.BirthdaySeason,
            BirthdayDay = current.BirthdayDay,
            FavoriteGiftItemIds = normalized
        };

        return this.SendOrApplyRequest(request, out message);
    }

    public void HandleProfileUpdateRequestHost(PlayerProfileUpdateRequestMessage request, long senderId)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        if (senderId != request.FromPlayerId)
        {
            this.mod.NetSync.SendError(senderId, "profile_sender_mismatch", "Profile update rejected: sender mismatch.");
            return;
        }

        Farmer? sender = this.mod.FindFarmerById(senderId, includeOffline: false);
        if (sender is null)
        {
            this.mod.NetSync.SendError(senderId, "profile_sender_offline", "Profile update rejected: sender not online.");
            return;
        }

        string normalizedSeason = NormalizeSeason(request.BirthdaySeason);
        if (!IsValidBirthday(normalizedSeason, request.BirthdayDay, out string birthdayError))
        {
            this.mod.NetSync.SendError(senderId, "profile_birthday_invalid", birthdayError);
            return;
        }

        if (!TryNormalizeFavoriteIds(request.FavoriteGiftItemIds, out List<string> normalizedFavorites, out string favoriteError))
        {
            this.mod.NetSync.SendError(senderId, "profile_favorites_invalid", favoriteError);
            return;
        }

        PlayerProfileRecord profile = new()
        {
            PlayerId = senderId,
            PlayerName = sender.Name,
            BirthdaySeason = normalizedSeason,
            BirthdayDay = request.BirthdayDay,
            FavoriteGiftItemIds = normalizedFavorites
        };

        this.mod.HostSaveData.PlayerProfilesById[senderId] = profile;
        this.mod.MarkDataDirty("Player profile updated.", flushNow: true);
        this.mod.NetSync.Broadcast(MessageType.ProfileUpdated, new PlayerProfileUpdatedMessage { Profile = Clone(profile) });
        this.mod.NetSync.BroadcastSnapshotToAll();

        if (this.mod.Config.EnableProfileSaveHudMessage)
        {
            if (senderId == this.mod.LocalPlayerId)
            {
                Game1.addHUDMessage(new HUDMessage("Profile saved.", HUDMessage.newQuest_type));
            }

            this.mod.NetSync.SendToPlayer(
                MessageType.ProfileUpdated,
                new PlayerProfileUpdatedMessage { Profile = Clone(profile) },
                senderId);
        }
    }

    public void ApplyProfileUpdatedClient(PlayerProfileRecord updated)
    {
        if (updated.PlayerId <= 0)
        {
            return;
        }

        Farmer? farmer = this.mod.FindFarmerById(updated.PlayerId, includeOffline: true);
        if (farmer is not null)
        {
            updated.PlayerName = farmer.Name;
        }

        if (this.mod.IsHostPlayer)
        {
            this.mod.HostSaveData.PlayerProfilesById[updated.PlayerId] = Clone(updated);
        }

        this.mod.ClientSnapshot.PlayerProfiles.RemoveAll(p => p.PlayerId == updated.PlayerId);
        this.mod.ClientSnapshot.PlayerProfiles.Add(Clone(updated));

        if (this.mod.Config.EnableProfileSaveHudMessage && this.mod.LocalPlayerId == updated.PlayerId)
        {
            Game1.addHUDMessage(new HUDMessage("Profile saved.", HUDMessage.newQuest_type));
        }
    }

    public PlayerProfileRecord? GetProfile(long playerId)
    {
        if (playerId <= 0)
        {
            return null;
        }

        if (this.mod.IsHostPlayer && this.mod.HostSaveData.PlayerProfilesById.TryGetValue(playerId, out PlayerProfileRecord? hostProfile))
        {
            return hostProfile;
        }

        return this.mod.ClientSnapshot.PlayerProfiles.FirstOrDefault(p => p.PlayerId == playerId);
    }

    public string DumpProfile(long playerId)
    {
        PlayerProfileRecord? profile = this.GetProfile(playerId);
        if (profile is null)
        {
            return $"Profile not found for player {playerId}.";
        }

        string birthday = string.IsNullOrWhiteSpace(profile.BirthdaySeason) || profile.BirthdayDay <= 0
            ? "Unknown"
            : $"{ToDisplaySeason(profile.BirthdaySeason)} {profile.BirthdayDay}";
        string favorites = profile.FavoriteGiftItemIds.Count == 0
            ? "Not set"
            : string.Join(", ", profile.FavoriteGiftItemIds);

        return $"profile player={profile.PlayerName}({profile.PlayerId}) birthday={birthday} favorites={favorites}";
    }

    public static string ToDisplaySeason(string season)
    {
        if (string.IsNullOrWhiteSpace(season))
        {
            return "Unknown";
        }

        string normalized = season.Trim().ToLowerInvariant();
        return char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }

    private bool SendOrApplyRequest(PlayerProfileUpdateRequestMessage request, out string message)
    {
        if (!Context.IsWorldReady)
        {
            message = "World not ready.";
            return false;
        }

        if (this.mod.IsHostPlayer)
        {
            this.HandleProfileUpdateRequestHost(request, this.mod.LocalPlayerId);
            message = "Profile update applied.";
            return true;
        }

        this.mod.NetSync.SendToPlayer(MessageType.ProfileUpdateRequest, request, Game1.MasterPlayer.UniqueMultiplayerID);
        message = "Profile update request sent to host.";
        return true;
    }

    private PlayerProfileRecord GetOrCreateLocalSeedProfile()
    {
        PlayerProfileRecord? existing = this.GetProfile(this.mod.LocalPlayerId);
        if (existing is not null)
        {
            return existing;
        }

        return new PlayerProfileRecord
        {
            PlayerId = this.mod.LocalPlayerId,
            PlayerName = this.mod.LocalPlayerName,
            BirthdaySeason = string.Empty,
            BirthdayDay = 0,
            FavoriteGiftItemIds = new List<string>()
        };
    }

    private static bool IsValidBirthday(string normalizedSeason, int day, out string error)
    {
        if (!ValidSeasons.Contains(normalizedSeason))
        {
            error = "Birthday season must be spring, summer, fall, or winter.";
            return false;
        }

        if (day < 1 || day > 28)
        {
            error = "Birthday day must be between 1 and 28.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryNormalizeFavoriteIds(IEnumerable<string> rawIds, out List<string> normalized, out string error)
    {
        normalized = new List<string>();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string candidate in rawIds)
        {
            string id = NormalizeQualifiedId(candidate);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!IsVanillaQualifiedItemId(id) || !IsValidVanillaItem(id))
            {
                this.mod.Monitor.Log($"[PR.System.Profile] Rejected invalid favorite item id '{candidate}'.", LogLevel.Warn);
                continue;
            }

            if (seen.Add(id))
            {
                normalized.Add(id);
            }
        }

        if (normalized.Count > 32)
        {
            normalized = normalized.Take(32).ToList();
        }

        error = string.Empty;
        return true;
    }

    private static bool IsVanillaQualifiedItemId(string qualifiedId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedId) || qualifiedId.Length < 4 || qualifiedId[0] != '(')
        {
            return false;
        }

        int close = qualifiedId.IndexOf(')');
        if (close <= 1 || close >= qualifiedId.Length - 1)
        {
            return false;
        }

        string valuePart = qualifiedId[(close + 1)..];
        return int.TryParse(valuePart, out _);
    }

    private static bool IsValidVanillaItem(string qualifiedId)
    {
        try
        {
            Item? item = ItemRegistry.Create(qualifiedId, 1, 0, allowNull: true);
            return item is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeQualifiedId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string id = raw.Trim();
        if (id.StartsWith("(", StringComparison.OrdinalIgnoreCase))
        {
            return id;
        }

        if (int.TryParse(id, out int objectId))
        {
            return $"(O){objectId}";
        }

        return id;
    }

    private static string NormalizeSeason(string season)
    {
        return string.IsNullOrWhiteSpace(season)
            ? string.Empty
            : season.Trim().ToLowerInvariant();
    }

    private static PlayerProfileRecord Clone(PlayerProfileRecord profile)
    {
        return new PlayerProfileRecord
        {
            PlayerId = profile.PlayerId,
            PlayerName = profile.PlayerName,
            BirthdaySeason = profile.BirthdaySeason,
            BirthdayDay = profile.BirthdayDay,
            FavoriteGiftItemIds = new List<string>(profile.FavoriteGiftItemIds)
        };
    }
}
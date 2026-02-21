using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.Patches;

public static class FarmerPatches
{
    private static ModEntry? mod;

    private sealed class GiftPatchState
    {
        public Farmer? Giver;
        public Item? ItemBefore;
        public int BeforeStack;
    }

    public static void Initialize(ModEntry entry)
    {
        mod = entry;
        try
        {
            Harmony harmony = entry.Harmony ?? new Harmony(entry.ModManifest.UniqueID);
            var targets = ResolveGiftTargetMethods().ToList();
            if (targets.Count == 0)
            {
                entry.Monitor.Log("[PR.Patch.Farmer] Could not find vanilla player-transfer method to observe gifts.", LogLevel.Warn);
                return;
            }

            foreach (var target in targets)
            {
                harmony.Patch(
                    target,
                    prefix: new HarmonyMethod(typeof(FarmerPatches), nameof(TakeObjectFromPlayer_Prefix)),
                    postfix: new HarmonyMethod(typeof(FarmerPatches), nameof(TakeObjectFromPlayer_Postfix)));
            }

            entry.Monitor.Log($"[PR.Patch.Farmer] Gift observer patch applied to {targets.Count} method(s): {string.Join(", ", targets.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase))}", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            entry.Monitor.Log($"[PR.Patch.Farmer] Failed to initialize: {ex.Message}", LogLevel.Error);
        }
    }

    private static IEnumerable<System.Reflection.MethodInfo> ResolveGiftTargetMethods()
    {
        HashSet<System.Reflection.MethodInfo> results = new();
        string[] preferredNames =
        {
            "takeObjectFromPlayer",
            "TakeObjectFromPlayer",
            "takeItemFromPlayer",
            "TakeItemFromPlayer"
        };

        foreach (string name in preferredNames)
        {
            var method = AccessTools.Method(typeof(Farmer), name);
            if (method is not null)
            {
                results.Add(method);
            }
        }

        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Farmer)))
        {
            if (results.Contains(method))
            {
                continue;
            }

            bool looksLikeTransferByName =
                method.Name.Contains("FromPlayer", StringComparison.OrdinalIgnoreCase)
                && (method.Name.Contains("take", StringComparison.OrdinalIgnoreCase)
                    || method.Name.Contains("item", StringComparison.OrdinalIgnoreCase)
                    || method.Name.Contains("object", StringComparison.OrdinalIgnoreCase));

            if (!looksLikeTransferByName)
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                continue;
            }

            bool hasFarmerParam = parameters.Any(p => p.ParameterType == typeof(Farmer));
            if (!hasFarmerParam)
            {
                continue;
            }

            results.Add(method);
        }

        return results;
    }

    private static void TakeObjectFromPlayer_Prefix(Farmer __instance, object[] __args, out GiftPatchState __state)
    {
        __state = new GiftPatchState();

        Farmer? giver = __args.OfType<Farmer>().FirstOrDefault(p => p.UniqueMultiplayerID != __instance.UniqueMultiplayerID);
        if (giver is null)
        {
            return;
        }

        __state.Giver = giver;
        __state.ItemBefore = giver.CurrentItem?.getOne();
        __state.BeforeStack = giver.CurrentItem?.Stack ?? 0;
    }

    private static void TakeObjectFromPlayer_Postfix(Farmer __instance, object[] __args, object? __result, GiftPatchState __state)
    {
        if (mod is null || !Context.IsWorldReady || !mod.Config.EnableGiftDetection)
        {
            return;
        }

        if (__result is bool success && !success)
        {
            return;
        }

        Farmer receiver = __instance;
        Farmer? giver = __state.Giver ?? __args.OfType<Farmer>().FirstOrDefault(p => p.UniqueMultiplayerID != receiver.UniqueMultiplayerID);
        if (giver is null)
        {
            return;
        }

        Item? item = __args.OfType<Item>().FirstOrDefault() ?? __state.ItemBefore;
        if (item is null)
        {
            return;
        }

        int quantity = Math.Max(1, __state.BeforeStack > 0 && giver.CurrentItem is not null
            ? Math.Max(1, __state.BeforeStack - giver.CurrentItem.Stack)
            : item.Stack);

        mod.GiftTrackingSystem.TryProcessGift(giver, receiver, item, quantity, "takeObjectFromPlayer", out _);
    }
}

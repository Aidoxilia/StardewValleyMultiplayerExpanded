using PlayerRomance.Data;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.Systems;

public sealed class DateVendorShopService
{
    private const string AssetPath = "assets/data/date_vendor_stock.json";
    private readonly ModEntry mod;
    private readonly HashSet<string> dateGiftItemIds = new(StringComparer.OrdinalIgnoreCase);
    private List<DateVendorStockEntry> loadedEntries = new();

    public DateVendorShopService(ModEntry mod)
    {
        this.mod = mod;
    }

    public void LoadStock()
    {
        List<DateVendorStockEntry>? fromFile;
        try
        {
            fromFile = this.mod.Helper.ModContent.Load<List<DateVendorStockEntry>>(AssetPath);
        }
        catch (Exception ex)
        {
            this.mod.Monitor.Log($"[PR.System.VendorShop] Failed to load {AssetPath}: {ex.Message}", LogLevel.Warn);
            this.loadedEntries = new();
            this.dateGiftItemIds.Clear();
            return;
        }

        this.loadedEntries = fromFile ?? new();
        this.RebuildDateGiftTags();
        this.mod.Monitor.Log($"[PR.System.VendorShop] Loaded {this.loadedEntries.Count} stock entries.", LogLevel.Trace);
    }

    public void InvalidateAndReloadIfNeeded(IEnumerable<IAssetName> namesWithoutLocale)
    {
        if (namesWithoutLocale.Any(name =>
            string.Equals(name.BaseName, $"Mods/{this.mod.ModManifest.UniqueID}/{AssetPath}", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name.BaseName, AssetPath, StringComparison.OrdinalIgnoreCase)))
        {
            this.LoadStock();
        }
    }

    public bool IsDateGiftItem(string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return false;
        }

        if (this.dateGiftItemIds.Contains(qualifiedItemId))
        {
            return true;
        }

        return this.mod.Config.AdditionalDateGiftItemIds
            .Select(NormalizeQualifiedId)
            .Any(id => string.Equals(id, qualifiedItemId, StringComparison.OrdinalIgnoreCase));
    }

    public bool TryOpenVendorShop(out string message)
    {
        if (!Context.IsWorldReady)
        {
            message = "World not ready.";
            return false;
        }

        Dictionary<ISalable, ItemStockInformation> stock = this.BuildStockForToday();
        if (stock.Count == 0)
        {
            message = "No available vendor stock for today.";
            return false;
        }

        try
        {
            IClickableMenu? menu = this.CreateShopMenu(stock);
            if (menu is null)
            {
                message = "Vendor shop API unavailable for this game build.";
                return false;
            }

            Game1.activeClickableMenu = menu;
            message = "Vendor shop opened.";
            return true;
        }
        catch (Exception ex)
        {
            this.mod.Monitor.Log($"[PR.System.VendorShop] Failed to open shop: {ex.Message}", LogLevel.Warn);
            message = "Vendor shop unavailable right now.";
            return false;
        }
    }

    private Dictionary<ISalable, ItemStockInformation> BuildStockForToday()
    {
        Dictionary<ISalable, ItemStockInformation> stock = new();
        string season = Game1.currentSeason ?? string.Empty;
        int day = Game1.dayOfMonth;

        foreach (DateVendorStockEntry entry in this.loadedEntries)
        {
            string normalized = NormalizeQualifiedId(entry.ItemId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (!PassesCondition(entry, season, day))
            {
                continue;
            }

            Item? item;
            try
            {
                item = ItemRegistry.Create(normalized, 1, 0, allowNull: true);
            }
            catch (Exception ex)
            {
                this.mod.Monitor.Log($"[PR.System.VendorShop] Failed to resolve item id '{entry.ItemId}': {ex.Message}", LogLevel.Warn);
                continue;
            }

            if (item is not ISalable salable)
            {
                this.mod.Monitor.Log($"[PR.System.VendorShop] Invalid item id '{entry.ItemId}' in stock JSON. Entry skipped.", LogLevel.Warn);
                continue;
            }

            int price = Math.Max(0, entry.Price ?? item.salePrice());
            int qty = entry.Stock ?? int.MaxValue;
            ItemStockInformation? info = CreateStockInfo(price, qty);
            if (info is null)
            {
                continue;
            }

            stock[salable] = info;
        }

        return stock;
    }

    private IClickableMenu? CreateShopMenu(Dictionary<ISalable, ItemStockInformation> stock)
    {
        var ctor = typeof(ShopMenu).GetConstructors()
            .FirstOrDefault(c =>
            {
                var p = c.GetParameters();
                return p.Length >= 2
                    && p[0].ParameterType == typeof(string)
                    && p[1].ParameterType.IsAssignableFrom(stock.GetType());
            });

        if (ctor is null)
        {
            this.mod.Monitor.Log("[PR.System.VendorShop] Could not find compatible ShopMenu constructor.", LogLevel.Warn);
            return null;
        }

        var parameters = ctor.GetParameters();
        object?[] args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : GetDefault(parameters[i].ParameterType);
        }

        args[0] = "DateVendor";
        args[1] = stock;
        if (parameters.Length >= 3 && parameters[2].ParameterType == typeof(int))
        {
            args[2] = 0;
        }

        return ctor.Invoke(args) as IClickableMenu;
    }

    private static ItemStockInformation? CreateStockInfo(int price, int stock)
    {
        var ctor = typeof(ItemStockInformation).GetConstructors().FirstOrDefault();
        if (ctor is null)
        {
            return null;
        }

        var p = ctor.GetParameters();
        if (p.Length < 9)
        {
            return null;
        }

        object limitedMode = p[4].ParameterType.IsEnum
            ? Enum.ToObject(p[4].ParameterType, 0)
            : GetDefault(p[4].ParameterType)!;

        object?[] args =
        {
            price,
            stock,
            null,
            null,
            limitedMode,
            null,
            null,
            null,
            new List<string>()
        };

        return ctor.Invoke(args) as ItemStockInformation;
    }

    private static object? GetDefault(Type type)
    {
        if (!type.IsValueType)
        {
            return null;
        }

        return Activator.CreateInstance(type);
    }

    private void RebuildDateGiftTags()
    {
        this.dateGiftItemIds.Clear();
        foreach (DateVendorStockEntry entry in this.loadedEntries)
        {
            string id = NormalizeQualifiedId(entry.ItemId);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (entry.Tags.Any(tag => string.Equals(tag, "DateGift", StringComparison.OrdinalIgnoreCase)))
            {
                this.dateGiftItemIds.Add(id);
            }
        }
    }

    private static bool PassesCondition(DateVendorStockEntry entry, string season, int day)
    {
        if (!string.IsNullOrWhiteSpace(entry.Season)
            && !string.Equals(entry.Season, season, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(entry.Season, "any", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (entry.MinDay.HasValue && day < entry.MinDay.Value)
        {
            return false;
        }

        if (entry.MaxDay.HasValue && day > entry.MaxDay.Value)
        {
            return false;
        }

        return true;
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
}

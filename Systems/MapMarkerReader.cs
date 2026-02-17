using Microsoft.Xna.Framework;
using StardewValley;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace PlayerRomance.Systems;

public sealed class MarkerInfo
{
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int TileX { get; set; }
    public int TileY { get; set; }
}

public sealed class MapMarkerReader
{
    private readonly ModEntry mod;

    public MapMarkerReader(ModEntry mod)
    {
        this.mod = mod;
    }

    public Dictionary<string, MarkerInfo> ReadMarkers(GameLocation location)
    {
        Dictionary<string, MarkerInfo> markers = new(StringComparer.OrdinalIgnoreCase);
        if (location?.Map is null)
        {
            return markers;
        }

        Layer? layer = location.Map.Layers.FirstOrDefault(p => p.Id.Equals("Markers", StringComparison.OrdinalIgnoreCase))
                       ?? location.Map.Layers.FirstOrDefault(p => p.Id.Contains("Marker", StringComparison.OrdinalIgnoreCase));

        if (layer is null)
        {
            this.mod.Monitor.Log("[PR.System.DateEvent] Marker layer not found, using fallback spots.", StardewModdingAPI.LogLevel.Warn);
            return markers;
        }

        for (int x = 0; x < layer.LayerWidth; x++)
        {
            for (int y = 0; y < layer.LayerHeight; y++)
            {
                Tile? tile = layer.Tiles[x, y];
                if (tile is null)
                {
                    continue;
                }

                string name = GetProperty(tile, "Name")
                              ?? GetProperty(tile, "name")
                              ?? GetProperty(tile, "MarkerName")
                              ?? tile.TileIndex.ToString();
                string markerClass = GetProperty(tile, "Class")
                                     ?? GetProperty(tile, "class")
                                     ?? GetProperty(tile, "Type")
                                     ?? GetProperty(tile, "type")
                                     ?? string.Empty;

                if (!IsSupportedMarkerClass(markerClass))
                {
                    if (name.StartsWith("Player", StringComparison.OrdinalIgnoreCase))
                    {
                        markerClass = "Player_Spot";
                    }
                    else if (name.StartsWith("Npc_Spot", StringComparison.OrdinalIgnoreCase))
                    {
                        markerClass = "Npc_Spot";
                    }
                }

                if (!IsSupportedMarkerClass(markerClass) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                markers[name] = new MarkerInfo
                {
                    Name = name,
                    Class = markerClass,
                    TileX = x,
                    TileY = y
                };
            }
        }

        this.mod.Monitor.Log($"[PR.System.DateEvent] Read {markers.Count} marker(s) on map '{location.NameOrUniqueName}'.", StardewModdingAPI.LogLevel.Trace);
        return markers;
    }

    public static Vector2 GetMarkerTileOrFallback(IReadOnlyDictionary<string, MarkerInfo> markers, string markerName, Vector2 fallback)
    {
        if (markers.TryGetValue(markerName, out MarkerInfo? marker))
        {
            return new Vector2(marker.TileX, marker.TileY);
        }

        return fallback;
    }

    private static bool IsSupportedMarkerClass(string markerClass)
    {
        return markerClass.Equals("Player_Spot", StringComparison.OrdinalIgnoreCase)
               || markerClass.Equals("Npc_Spot", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProperty(Tile tile, string key)
    {
        try
        {
            if (tile.Properties.TryGetValue(key, out var value))
            {
                return value.ToString();
            }
        }
        catch
        {
        }

        return null;
    }
}

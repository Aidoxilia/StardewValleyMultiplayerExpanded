using Microsoft.Xna.Framework;
using StardewValley;
using System.Globalization;
using System.Xml.Linq;
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

        this.ReadFromTmxObjectLayer(location, markers);
        if (markers.Count > 0)
        {
            this.mod.Monitor.Log($"[PR.System.DateEvent] Read {markers.Count} marker(s) from TMX object layer for '{location.NameOrUniqueName}'.", StardewModdingAPI.LogLevel.Trace);
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

    private void ReadFromTmxObjectLayer(GameLocation location, IDictionary<string, MarkerInfo> markers)
    {
        string fileName = $"{location.NameOrUniqueName}.tmx";
        string mapPath = Path.Combine(this.mod.Helper.DirectoryPath, "assets", "Maps", fileName);
        if (!File.Exists(mapPath))
        {
            return;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(mapPath);
        }
        catch
        {
            return;
        }

        XElement? markerGroup = doc.Root?
            .Elements("objectgroup")
            .FirstOrDefault(p => string.Equals((string?)p.Attribute("name"), "Markers", StringComparison.OrdinalIgnoreCase))
            ?? doc.Root?
                .Elements("objectgroup")
                .FirstOrDefault(p => ((string?)p.Attribute("name"))?.Contains("Marker", StringComparison.OrdinalIgnoreCase) == true);
        if (markerGroup is null)
        {
            return;
        }

        float tileSize = ParseFloat((string?)doc.Root?.Attribute("tilewidth"), 16f);
        foreach (XElement obj in markerGroup.Elements("object"))
        {
            string name = ((string?)obj.Attribute("name") ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string markerClass = ((string?)obj.Attribute("class") ?? (string?)obj.Attribute("type") ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(markerClass))
            {
                markerClass = ReadObjectProperty(obj, "Class")
                              ?? ReadObjectProperty(obj, "class")
                              ?? ReadObjectProperty(obj, "Type")
                              ?? ReadObjectProperty(obj, "type")
                              ?? string.Empty;
            }

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

            if (!IsSupportedMarkerClass(markerClass))
            {
                continue;
            }

            float x = ParseFloat((string?)obj.Attribute("x"), 0f);
            float y = ParseFloat((string?)obj.Attribute("y"), 0f);
            float objectHeight = ParseFloat((string?)obj.Attribute("height"), tileSize);

            // Tiled object coordinates are pixel-based and y is anchored at the object bottom for tile objects.
            // Convert to gameplay tile coordinates per Stardew map conventions.
            // Ref: https://stardewvalleywiki.com/Modding:Maps#Tile_coordinates
            int tileX = (int)Math.Floor(x / Math.Max(1f, tileSize));
            int tileY = (int)Math.Floor((y - Math.Max(1f, objectHeight)) / Math.Max(1f, tileSize));

            markers[name] = new MarkerInfo
            {
                Name = name,
                Class = markerClass,
                TileX = tileX,
                TileY = tileY
            };
        }
    }

    private static string? ReadObjectProperty(XElement obj, string key)
    {
        XElement? property = obj.Element("properties")?
            .Elements("property")
            .FirstOrDefault(p => string.Equals((string?)p.Attribute("name"), key, StringComparison.OrdinalIgnoreCase));
        if (property is null)
        {
            return null;
        }

        return ((string?)property.Attribute("value") ?? property.Value)?.Trim();
    }

    private static float ParseFloat(string? value, float fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            return parsed;
        }

        return fallback;
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

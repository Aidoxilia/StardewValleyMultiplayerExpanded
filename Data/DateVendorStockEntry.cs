namespace PlayerRomance.Data;

public sealed class DateVendorStockEntry
{
    public string ItemId { get; set; } = string.Empty;
    public int? Price { get; set; }
    public int? Stock { get; set; }
    public string? Season { get; set; }
    public int? MinDay { get; set; }
    public int? MaxDay { get; set; }
    public List<string> Tags { get; set; } = new();
}

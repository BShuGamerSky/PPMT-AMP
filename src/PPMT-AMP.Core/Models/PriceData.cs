namespace PPMT_AMP.Core.Models;

/// <summary>
/// Model representing after-market price data
/// </summary>
public class PriceData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal MarketPrice { get; set; }
    public decimal RetailPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime PriceDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Active";
}

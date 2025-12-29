namespace PPMT_AMP.Core.Models;

/// <summary>
/// Model representing individual PopMart blind box items with after-market pricing
/// </summary>
public class PpmtItem
{
    // Primary Keys
    public string SeriesId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    
    // Product Information
    public string ProductName { get; set; } = string.Empty;
    public string IpCharacter { get; set; } = string.Empty;
    public string SeriesName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    // Pricing Information
    public decimal RetailPrice { get; set; }
    public decimal AfterMarketPrice { get; set; }
    public string Currency { get; set; } = "CNY";
    public decimal PriceChange { get; set; }
    public decimal PriceChangePercent { get; set; }
    
    // Metadata
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
    public string Status { get; set; } = "Active";
    public string Rarity { get; set; } = "Common";
    public int SeriesSize { get; set; }
    
    // Additional Fields
    public string ImageUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Legacy compatibility (for backward compatibility during migration)
    [Obsolete("Use AfterMarketPrice instead")]
    public decimal MarketPrice => AfterMarketPrice;
    
    [Obsolete("Use Timestamp instead")]
    public DateTime PriceDate => DateTime.TryParse(Timestamp, out var dt) ? dt : DateTime.UtcNow;
}

/// <summary>
/// Model representing PopMart blind box series information
/// </summary>
public class PpmtSeries
{
    // Primary Key
    public string SeriesId { get; set; } = string.Empty;
    
    // Series Information
    public string SeriesName { get; set; } = string.Empty;
    public string IpCharacter { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Release Information
    public string? ReleaseDate { get; set; } // Store as string for flexibility (e.g., "2024-12")
    public string Status { get; set; } = "Active"; // Active, Pre-Order, Discontinued
    
    // Series Details
    public int TotalItems { get; set; } // Total figures in series (e.g., 12)
    public List<string> IncludedItems { get; set; } = new List<string>(); // List of ProductIds in this series
    public List<string> RelatedIpCharacters { get; set; } = new List<string>(); // Related IP characters
    
    // Pricing Information
    public decimal RetailPrice { get; set; } // Blind box retail price
    public string Currency { get; set; } = "CNY";
    
    // Visual Assets
    public string ImageUrl { get; set; } = string.Empty; // Series box/banner image
    public string ThumbnailUrl { get; set; } = string.Empty;
    public List<string> GalleryImages { get; set; } = new List<string>(); // Additional images
    
    // Metadata
    public string Manufacturer { get; set; } = "Pop Mart";
    public string Region { get; set; } = "CN"; // CN, US, EU, etc.
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
    
    // TTL for automatic expiration
    public long TTL { get; set; }
}

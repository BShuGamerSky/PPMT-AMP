namespace PPMT_AMP.Core.Models;

/// <summary>
/// API request with signature for verification
/// </summary>
public class ApiRequest
{
    public string AppId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public string Signature { get; set; } = string.Empty;
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Price query request
/// </summary>
public class PriceQueryRequest : ApiRequest
{
    public string? ProductId { get; set; }
    public string? Category { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public int? RateLimitRemaining { get; set; }
    public DateTime? RateLimitReset { get; set; }
}

/// <summary>
/// User role enum
/// </summary>
public enum UserRole
{
    Visitor,      // Read-only access
    Registered,   // Full access (CRUD)
    Admin         // Full access + admin features
}

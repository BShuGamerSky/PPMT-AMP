using System.Security.Cryptography;
using System.Text;

namespace PPMT_AMP.Core.Services;

/// <summary>
/// API client for secure communication with backend
/// Implements app signature verification and rate limiting
/// </summary>
public class ApiClient
{
    private static ApiClient? _instance;
    private readonly string _apiBaseUrl;
    private readonly string _appId;
    private readonly string _appSecret;
    private string _deviceId;
    private readonly HttpClient _httpClient;

    // Rate limiting tracking
    private int _requestCount = 0;
    private DateTime _rateLimitResetTime = DateTime.UtcNow.AddMinutes(5);

    private ApiClient()
    {
        // TODO: Load from configuration file
        // For now, use placeholder - update this after AWS setup
        _apiBaseUrl = "https://YOUR_API_ID.execute-api.us-east-1.amazonaws.com/prod";
        _appId = "ppmt-amp-ios-v1"; // Unique app identifier
        _appSecret = "your-secret-key-change-this-in-production"; // Secret for signing requests
        _deviceId = GetOrCreateDeviceId();
        _httpClient = new HttpClient();
        
        // Load from config if available
        var config = Configuration.AppConfiguration.Instance;
        var apiUrl = config.GetValue("AWS.ApiGateway.BaseUrl");
        if (!string.IsNullOrEmpty(apiUrl))
        {
            _apiBaseUrl = apiUrl;
            Console.WriteLine($"API URL loaded from config: {_apiBaseUrl}");
        }
        
        var appSecret = config.GetValue("AWS.ApiGateway.AppSecret");
        if (!string.IsNullOrEmpty(appSecret))
        {
            _appSecret = appSecret;
            Console.WriteLine($"AppSecret loaded from config (length: {_appSecret.Length})");
        }
        else
        {
            Console.WriteLine($"WARNING: AppSecret not found in config, using default");
        }
    }

    public static ApiClient Instance
    {
        get
        {
            _instance ??= new ApiClient();
            return _instance;
        }
    }

    /// <summary>
    /// Get or create unique device identifier
    /// </summary>
    private string GetOrCreateDeviceId()
    {
        // In production, use iOS device UDID or generate persistent UUID
        var deviceId = Guid.NewGuid().ToString();
        // TODO: Store in iOS Keychain for persistence
        return deviceId;
    }

    /// <summary>
    /// Generate HMAC-SHA256 signature for request verification
    /// </summary>
    private string GenerateSignature(string payload, long timestamp)
    {
        var message = $"{_appId}:{_deviceId}:{timestamp}:{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Check if rate limit allows request
    /// </summary>
    private bool CheckRateLimit()
    {
        if (DateTime.UtcNow > _rateLimitResetTime)
        {
            _requestCount = 0;
            _rateLimitResetTime = DateTime.UtcNow.AddMinutes(5);
        }

        if (_requestCount >= 20)
        {
            return false;
        }

        _requestCount++;
        return true;
    }

    /// <summary>
    /// Query prices (available to visitors)
    /// </summary>
    public async Task<Models.ApiResponse<List<Models.PriceData>>> QueryPricesAsync(
        string? productId = null,
        string? category = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 50)
    {
        try
        {
            // Check rate limit
            if (!CheckRateLimit())
            {
                return new Models.ApiResponse<List<Models.PriceData>>
                {
                    Success = false,
                    Message = "Rate limit exceeded. Please try again later.",
                    RateLimitRemaining = 0,
                    RateLimitReset = _rateLimitResetTime
                };
            }

            // Create request payload
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Hybrid approach: Sign method + path for better security
            var payload = "GET:/prices";
            var signature = GenerateSignature(payload, timestamp);

            // Build query parameters
            var queryParams = new Dictionary<string, string>
            {
                ["appId"] = _appId,
                ["deviceId"] = _deviceId,
                ["timestamp"] = timestamp.ToString(),
                ["signature"] = signature
            };

            if (!string.IsNullOrEmpty(productId))
                queryParams["productId"] = productId;
            if (!string.IsNullOrEmpty(category))
                queryParams["category"] = category;
            if (startDate.HasValue)
                queryParams["startDate"] = startDate.Value.ToString("yyyy-MM-dd");
            if (endDate.HasValue)
                queryParams["endDate"] = endDate.Value.ToString("yyyy-MM-dd");
            queryParams["limit"] = limit.ToString();

            // Build URL with query string
            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var url = $"{_apiBaseUrl}/prices?{queryString}";

            Console.WriteLine($"API Request: GET {url}");
            
            // Make request
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // TODO: Parse JSON response
                // For now, return mock data
                var mockData = GenerateMockPriceData();
                
                return new Models.ApiResponse<List<Models.PriceData>>
                {
                    Success = true,
                    Message = "Query successful",
                    Data = mockData,
                    RateLimitRemaining = 20 - _requestCount,
                    RateLimitReset = _rateLimitResetTime
                };
            }
            else
            {
                return new Models.ApiResponse<List<Models.PriceData>>
                {
                    Success = false,
                    Message = $"API error: {response.StatusCode}",
                    RateLimitRemaining = 20 - _requestCount,
                    RateLimitReset = _rateLimitResetTime
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Error: {ex.Message}");
            return new Models.ApiResponse<List<Models.PriceData>>
            {
                Success = false,
                Message = $"Request failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate mock price data for testing
    /// </summary>
    private List<Models.PriceData> GenerateMockPriceData()
    {
        var random = new Random();
        var products = new[] { "iPhone 15 Pro", "MacBook Pro M3", "AirPods Pro", "iPad Air", "Apple Watch Ultra" };
        var categories = new[] { "Electronics", "Computers", "Audio", "Tablets", "Wearables" };
        
        var data = new List<Models.PriceData>();
        for (int i = 0; i < 10; i++)
        {
            var productIndex = random.Next(products.Length);
            data.Add(new Models.PriceData
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = $"PROD-{1000 + i}",
                ProductName = products[productIndex],
                Category = categories[productIndex],
                MarketPrice = random.Next(500, 2000),
                RetailPrice = random.Next(450, 1900),
                Currency = "USD",
                PriceDate = DateTime.UtcNow.AddDays(-random.Next(0, 30)),
                Source = "Market Data",
                Status = "Active"
            });
        }
        
        return data;
    }

    /// <summary>
    /// Upload data (requires authentication)
    /// </summary>
    public async Task<Models.ApiResponse<bool>> UploadPriceDataAsync(Models.PriceData priceData)
    {
        if (!AuthService.Instance.IsAuthenticated)
        {
            return new Models.ApiResponse<bool>
            {
                Success = false,
                Message = "Authentication required. Please sign in to upload data."
            };
        }

        // Check rate limit
        if (!CheckRateLimit())
        {
            return new Models.ApiResponse<bool>
            {
                Success = false,
                Message = "Rate limit exceeded. Please try again later.",
                RateLimitRemaining = 0,
                RateLimitReset = _rateLimitResetTime
            };
        }

        // TODO: Implement upload with authentication token
        Console.WriteLine("Upload requires authentication - feature available for registered users");
        
        return new Models.ApiResponse<bool>
        {
            Success = false,
            Message = "Upload feature requires registration. Coming soon!",
            RateLimitRemaining = 20 - _requestCount
        };
    }

    /// <summary>
    /// Get current rate limit status
    /// </summary>
    public (int remaining, DateTime resetTime) GetRateLimitStatus()
    {
        if (DateTime.UtcNow > _rateLimitResetTime)
        {
            _requestCount = 0;
            _rateLimitResetTime = DateTime.UtcNow.AddMinutes(5);
        }
        
        return (20 - _requestCount, _rateLimitResetTime);
    }
}

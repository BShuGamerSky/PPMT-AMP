using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    /// Convert DateTime to string for API response
    /// </summary>
    private string FormatRateLimitReset(DateTime resetTime)
    {
        return resetTime.ToString("O"); // ISO 8601 format
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
    public async Task<Models.ApiResponse<List<Models.PpmtItem>>> QueryPricesAsync(
        string? seriesId = null,
        string? productId = null,
        string? ipCharacter = null,
        string? category = null,
        string? rarity = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 50)
    {
        try
        {
            // Check rate limit
            if (!CheckRateLimit())
            {
                return new Models.ApiResponse<List<Models.PpmtItem>>
                {
                    Success = false,
                    Message = "Rate limit exceeded. Please try again later.",
                    RateLimitRemaining = 0,
                    RateLimitReset = FormatRateLimitReset(_rateLimitResetTime)
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

            if (!string.IsNullOrEmpty(seriesId))
                queryParams["seriesId"] = seriesId;
            if (!string.IsNullOrEmpty(productId))
                queryParams["productId"] = productId;
            if (!string.IsNullOrEmpty(ipCharacter))
                queryParams["ipCharacter"] = ipCharacter;
            if (!string.IsNullOrEmpty(category))
                queryParams["category"] = category;
            if (!string.IsNullOrEmpty(rarity))
                queryParams["rarity"] = rarity;
            if (startDate.HasValue)
                queryParams["startDate"] = startDate.Value.ToString("yyyy-MM-dd");
            if (endDate.HasValue)
                queryParams["endDate"] = endDate.Value.ToString("yyyy-MM-dd");
            queryParams["limit"] = limit.ToString();

            // Build URL with query string
            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var url = $"{_apiBaseUrl}/prices?{queryString}";
            
            // Make request with timing
            var requestStart = DateTime.UtcNow;
            var response = await _httpClient.GetAsync(url);
            var networkTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
            
            var content = await response.Content.ReadAsStringAsync();
            var totalTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                // Parse JSON response
                var apiResponse = JsonSerializer.Deserialize<Models.ApiResponse<List<Models.PpmtItem>>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse != null)
                {
                    return apiResponse;
                }
                else
                {
                    // Fallback to mock data if parsing fails
                    var mockData = GenerateMockPriceData();
                    return new Models.ApiResponse<List<Models.PpmtItem>>
                    {
                        Success = true,
                        Message = "Query successful (mock data fallback)",
                        Data = mockData,
                        RateLimitRemaining = 20 - _requestCount,
                        RateLimitReset = FormatRateLimitReset(_rateLimitResetTime)
                    };
                }
            }
            else
            {
                return new Models.ApiResponse<List<Models.PpmtItem>>
                {
                    Success = false,
                    Message = $"API error: {response.StatusCode}",
                    RateLimitRemaining = 20 - _requestCount,
                    RateLimitReset = FormatRateLimitReset(_rateLimitResetTime)
                };
            }
        }
        catch (Exception ex)
        {
            return new Models.ApiResponse<List<Models.PpmtItem>>
            {
                Success = false,
                Message = $"Request failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate mock price data for testing
    /// </summary>
    private List<Models.PpmtItem> GenerateMockPriceData()
    {
        var random = new Random();
        var products = new[] { "Labubu Sitting", "Labubu Golden Monster", "Hirono Snowflakes", "Molly Racing", "Skullpanda City" };
        var series = new[] { "SERIES-LABUBU-MONSTERS", "SERIES-LABUBU-MONSTERS", "SERIES-HIRONO-WINTER2024", "SERIES-MOLLY-RACING", "SERIES-SKULLPANDA-CITY" };
        var ipCharacters = new[] { "Labubu", "Labubu", "Hirono", "Molly", "Skullpanda" };
        var rarities = new[] { "Common", "Secret", "Rare", "Common", "Chase" };
        
        var data = new List<Models.PpmtItem>();
        for (int i = 0; i < 10; i++)
        {
            var productIndex = random.Next(products.Length);
            data.Add(new Models.PpmtItem
            {
                SeriesId = series[productIndex],
                ProductId = $"PROD-{1000 + i}",
                ProductName = products[productIndex],
                IpCharacter = ipCharacters[productIndex],
                SeriesName = products[productIndex].Split(' ')[0] + " Series",
                Category = "Blind Box",
                RetailPrice = random.Next(50, 100),
                AfterMarketPrice = random.Next(80, 500),
                Currency = "CNY",
                PriceChange = 0,
                PriceChangePercent = 0,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Rarity = rarities[productIndex],
                Status = "Active",
                SeriesSize = 12
            });
        }
        
        return data;
    }

    /// <summary>
    /// Query series by IP character (available to visitors)
    /// </summary>
    public async Task<Models.ApiResponse<List<Models.PpmtSeries>>> QuerySeriesAsync(
        string? ipCharacter = null,
        string? seriesId = null,
        string? category = null,
        int limit = 50)
    {
        try
        {
            // Check rate limit
            if (!CheckRateLimit())
            {
                return new Models.ApiResponse<List<Models.PpmtSeries>>
                {
                    Success = false,
                    Message = "Rate limit exceeded. Please try again later.",
                    RateLimitRemaining = 0,
                    RateLimitReset = FormatRateLimitReset(_rateLimitResetTime)
                };
            }

            // Create request payload
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = "GET:/series";
            var signature = GenerateSignature(payload, timestamp);

            // Build query parameters
            var queryParams = new Dictionary<string, string>
            {
                ["appId"] = _appId,
                ["deviceId"] = _deviceId,
                ["timestamp"] = timestamp.ToString(),
                ["signature"] = signature
            };

            if (!string.IsNullOrEmpty(ipCharacter))
                queryParams["ipCharacter"] = ipCharacter;
            if (!string.IsNullOrEmpty(seriesId))
                queryParams["seriesId"] = seriesId;
            if (!string.IsNullOrEmpty(category))
                queryParams["category"] = category;
            queryParams["limit"] = limit.ToString();

            // Build URL with query string
            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var url = $"{_apiBaseUrl}/series?{queryString}";
            
            // Make request with timing
            var requestStart = DateTime.UtcNow;
            var response = await _httpClient.GetAsync(url);
            var networkTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
            
            var content = await response.Content.ReadAsStringAsync();
            var totalTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                // TODO: Parse JSON response
                // For now, return mock data
                var mockData = GenerateMockSeriesData(ipCharacter);
                
                return new Models.ApiResponse<List<Models.PpmtSeries>>
                {
                    Success = true,
                    Message = "Query successful",
                    Data = mockData,
                    RateLimitRemaining = 20 - _requestCount,
                    RateLimitReset = FormatRateLimitReset(_rateLimitResetTime)
                };
            }
            else
            {
                return new Models.ApiResponse<List<Models.PpmtSeries>>
                {
                    Success = false,
                    Message = $"API error: {response.StatusCode}",
                    RateLimitRemaining = 20 - _requestCount,
                    RateLimitReset = FormatRateLimitReset(_rateLimitResetTime)
                };
            }
        }
        catch (Exception ex)
        {
            return new Models.ApiResponse<List<Models.PpmtSeries>>
            {
                Success = false,
                Message = $"Request failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate mock series data for testing
    /// </summary>
    private List<Models.PpmtSeries> GenerateMockSeriesData(string? ipCharacter = null)
    {
        var allSeries = new List<Models.PpmtSeries>
        {
            // Labubu series
            new Models.PpmtSeries
            {
                SeriesId = "SERIES-LABUBU-001",
                SeriesName = "The Monsters",
                IpCharacter = "Labubu",
                Category = "Blind Box",
                Description = "Classic Labubu monster series",
                ReleaseDate = "2024-01",
                Status = "Active",
                TotalItems = 12,
                RetailPrice = 69,
                Currency = "CNY"
            },
            new Models.PpmtSeries
            {
                SeriesId = "SERIES-LABUBU-002",
                SeriesName = "Little Mischief",
                IpCharacter = "Labubu",
                Category = "Blind Box",
                Description = "Playful Labubu characters",
                ReleaseDate = "2024-06",
                Status = "Active",
                TotalItems = 10,
                RetailPrice = 69,
                Currency = "CNY"
            },
            // Hirono series
            new Models.PpmtSeries
            {
                SeriesId = "SERIES-HIRONO-001",
                SeriesName = "Winter Collection",
                IpCharacter = "Hirono",
                Category = "Blind Box",
                Description = "Winter-themed Hirono figures",
                ReleaseDate = "2023-12",
                Status = "Active",
                TotalItems = 8,
                RetailPrice = 69,
                Currency = "CNY"
            },
            new Models.PpmtSeries
            {
                SeriesId = "SERIES-HIRONO-002",
                SeriesName = "The Other One",
                IpCharacter = "Hirono",
                Category = "Blind Box",
                Description = "Alternative Hirono designs",
                ReleaseDate = "2024-03",
                Status = "Active",
                TotalItems = 9,
                RetailPrice = 69,
                Currency = "CNY"
            },
            new Models.PpmtSeries
            {
                SeriesId = "SERIES-HIRONO-003",
                SeriesName = "Little Princess",
                IpCharacter = "Hirono",
                Category = "Blind Box",
                Description = "Princess-themed Hirono series",
                ReleaseDate = "2024-08",
                Status = "Pre-Order",
                TotalItems = 10,
                RetailPrice = 69,
                Currency = "CNY"
            },
            // Molly series
            new Models.PpmtSeries
            {
                SeriesId = "SERIES-MOLLY-001",
                SeriesName = "Forest Fantasy",
                IpCharacter = "Molly",
                Category = "Blind Box",
                Description = "Molly in the enchanted forest",
                ReleaseDate = "2024-02",
                Status = "Active",
                TotalItems = 10,
                RetailPrice = 69,
                Currency = "CNY"
            },
            new Models.PpmtSeries
            {
                SeriesId = "SERIES-MOLLY-002",
                SeriesName = "Reshape",
                IpCharacter = "Molly",
                Category = "Blind Box",
                Description = "Redesigned classic Molly",
                ReleaseDate = "2024-05",
                Status = "Active",
                TotalItems = 12,
                RetailPrice = 69,
                Currency = "CNY"
            },
            // Skullpanda series
            new Models.PpmtSeries
            {
                SeriesId = "SERIES-SKULL-001",
                SeriesName = "City Night",
                IpCharacter = "Skullpanda",
                Category = "Blind Box",
                Description = "Urban night adventures",
                ReleaseDate = "2024-04",
                Status = "Active",
                TotalItems = 9,
                RetailPrice = 69,
                Currency = "CNY"
            }
        };

        // Filter by IP character if specified
        if (!string.IsNullOrEmpty(ipCharacter))
        {
            return allSeries.Where(s => s.IpCharacter.Equals(ipCharacter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return allSeries;
    }

    /// <summary>
    /// Upload data (requires authentication)
    /// </summary>
    public async Task<Models.ApiResponse<bool>> UploadPriceDataAsync(Models.PpmtItem priceData)
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
                RateLimitReset = FormatRateLimitReset(_rateLimitResetTime)
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

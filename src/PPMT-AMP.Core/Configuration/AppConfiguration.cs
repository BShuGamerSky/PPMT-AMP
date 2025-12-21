using Newtonsoft.Json.Linq;

namespace PPMT_AMP.Core.Configuration;

/// <summary>
/// Configuration manager for AWS and app settings
/// </summary>
public class AppConfiguration
{
    private static AppConfiguration? _instance;
    private JObject _config = new();

    private AppConfiguration()
    {
        LoadConfiguration();
    }

    public static AppConfiguration Instance
    {
        get
        {
            _instance ??= new AppConfiguration();
            return _instance;
        }
    }

    private void LoadConfiguration()
    {
        try
        {
            // Try to load from appsettings.json file
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                _config = JObject.Parse(json);
                Console.WriteLine($"Configuration loaded from: {configPath}");
            }
            else
            {
                Console.WriteLine($"Warning: Config file not found at {configPath}, using defaults");
                // Fallback to defaults
                _config = new JObject
                {
                    ["AWS"] = new JObject
                    {
                        ["Region"] = "us-east-1",
                        ["S3"] = new JObject
                        {
                            ["BucketName"] = "ppmt-amp-data-bucket",
                            ["DataPath"] = "market-prices/"
                        },
                        ["DynamoDB"] = new JObject
                        {
                            ["TableName"] = "PPMT-AMP-Prices",
                            ["IndexName"] = "DateIndex"
                        }
                    }
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            _config = new JObject();
        }
    }

    public string GetAWSRegion() => _config["AWS"]?["Region"]?.ToString() ?? "us-east-1";
    
    public string GetS3BucketName() => _config["AWS"]?["S3"]?["BucketName"]?.ToString() ?? "";
    
    public string GetS3DataPath() => _config["AWS"]?["S3"]?["DataPath"]?.ToString() ?? "";
    
    public string GetDynamoDBTableName() => _config["AWS"]?["DynamoDB"]?["TableName"]?.ToString() ?? "";
    
    public string GetDynamoDBIndexName() => _config["AWS"]?["DynamoDB"]?["IndexName"]?.ToString() ?? "";
    
    public string? GetValue(string path)
    {
        try
        {
            return _config.SelectToken(path)?.ToString();
        }
        catch
        {
            return null;
        }
    }
}

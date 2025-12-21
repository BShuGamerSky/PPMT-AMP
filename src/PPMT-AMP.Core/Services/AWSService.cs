using Amazon;
using Amazon.S3;
using Amazon.DynamoDBv2;
using Amazon.Lambda;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;

namespace PPMT_AMP.Core.Services;

/// <summary>
/// AWS Service Configuration and Client Management
/// </summary>
public class AWSService
{
    private static AWSService? _instance;
    private RegionEndpoint _region;
    
    // AWS Service Clients
    public IAmazonS3 S3Client { get; private set; } = null!;
    public IAmazonDynamoDB DynamoDBClient { get; private set; } = null!;
    public IAmazonLambda LambdaClient { get; private set; } = null!;
    public IAmazonCognitoIdentity CognitoIdentityClient { get; private set; } = null!;
    public IAmazonCognitoIdentityProvider CognitoIdentityProviderClient { get; private set; } = null!;

    private AWSService()
    {
        // Default to US-East-1, can be configured
        _region = RegionEndpoint.USEast1;
        InitializeClients();
    }

    public static AWSService Instance
    {
        get
        {
            _instance ??= new AWSService();
            return _instance;
        }
    }

        private void InitializeClients()
        {
            // Initialize AWS SDK clients
            // In production, use AWS Cognito for authentication
            var credentials = new AnonymousAWSCredentials(); // Replace with proper credentials
            
            S3Client = new AmazonS3Client(credentials, _region);
            DynamoDBClient = new AmazonDynamoDBClient(credentials, _region);
            LambdaClient = new AmazonLambdaClient(credentials, _region);
            CognitoIdentityClient = new AmazonCognitoIdentityClient(credentials, _region);
            CognitoIdentityProviderClient = new AmazonCognitoIdentityProviderClient(credentials, _region);
        }

        /// <summary>
        /// Configure AWS credentials
        /// </summary>
        public void ConfigureCredentials(string accessKey, string secretKey)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            
            S3Client = new AmazonS3Client(credentials, _region);
            DynamoDBClient = new AmazonDynamoDBClient(credentials, _region);
            LambdaClient = new AmazonLambdaClient(credentials, _region);
            CognitoIdentityClient = new AmazonCognitoIdentityClient(credentials, _region);
            CognitoIdentityProviderClient = new AmazonCognitoIdentityProviderClient(credentials, _region);
        }

    /// <summary>
    /// Configure AWS region
    /// </summary>
    public void ConfigureRegion(string regionName)
    {
        var region = RegionEndpoint.GetBySystemName(regionName);
        var credentials = new AnonymousAWSCredentials();
        
        S3Client = new AmazonS3Client(credentials, region);
        DynamoDBClient = new AmazonDynamoDBClient(credentials, region);
        LambdaClient = new AmazonLambdaClient(credentials, region);
        CognitoIdentityClient = new AmazonCognitoIdentityClient(credentials, region);
        CognitoIdentityProviderClient = new AmazonCognitoIdentityProviderClient(credentials, region);
    }
}

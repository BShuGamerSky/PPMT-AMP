using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace PPMT_AMP.Core.Services;

/// <summary>
/// Service for handling AWS DynamoDB operations
/// </summary>
public class DynamoDBService
{
    private readonly IAmazonDynamoDB _dynamoDBClient;

    public DynamoDBService()
    {
        _dynamoDBClient = AWSService.Instance.DynamoDBClient;
    }

    /// <summary>
    /// Put item into DynamoDB table
    /// </summary>
    public async Task<bool> PutItemAsync(string tableName, Dictionary<string, AttributeValue> item)
    {
        try
        {
            var request = new PutItemRequest
            {
                TableName = tableName,
                Item = item
            };

            await _dynamoDBClient.PutItemAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error putting item to DynamoDB: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get item from DynamoDB table
    /// </summary>
    public async Task<Dictionary<string, AttributeValue>?> GetItemAsync(string tableName, Dictionary<string, AttributeValue> key)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = tableName,
                Key = key
            };

            var response = await _dynamoDBClient.GetItemAsync(request);
            return response.Item;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting item from DynamoDB: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Query items from DynamoDB table
    /// </summary>
    public async Task<List<Dictionary<string, AttributeValue>>?> QueryAsync(string tableName, string keyConditionExpression, Dictionary<string, AttributeValue> expressionAttributeValues)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = tableName,
                KeyConditionExpression = keyConditionExpression,
                ExpressionAttributeValues = expressionAttributeValues
            };

            var response = await _dynamoDBClient.QueryAsync(request);
            return response.Items;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error querying DynamoDB: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Update item in DynamoDB table
    /// </summary>
    public async Task<bool> UpdateItemAsync(string tableName, Dictionary<string, AttributeValue> key, string updateExpression, Dictionary<string, AttributeValue> expressionAttributeValues)
    {
        try
        {
            var request = new UpdateItemRequest
            {
                TableName = tableName,
                Key = key,
                UpdateExpression = updateExpression,
                ExpressionAttributeValues = expressionAttributeValues
            };

            await _dynamoDBClient.UpdateItemAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating item in DynamoDB: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Delete item from DynamoDB table
    /// </summary>
    public async Task<bool> DeleteItemAsync(string tableName, Dictionary<string, AttributeValue> key)
    {
        try
        {
            var request = new DeleteItemRequest
            {
                TableName = tableName,
                Key = key
            };

            await _dynamoDBClient.DeleteItemAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting item from DynamoDB: {ex.Message}");
            return false;
        }
    }
}

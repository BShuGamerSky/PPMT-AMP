using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace PPMT_AMP.Core.Services;

/// <summary>
/// Service for handling AWS S3 operations
/// </summary>
public class S3Service
{
    private readonly IAmazonS3 _s3Client;

    public S3Service()
    {
        _s3Client = AWSService.Instance.S3Client;
    }

    /// <summary>
    /// Upload file to S3 bucket
    /// </summary>
    public async Task<bool> UploadFileAsync(string bucketName, string key, Stream fileStream)
    {
        try
        {
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                Key = key,
                BucketName = bucketName,
                CannedACL = S3CannedACL.Private
            };

            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadAsync(uploadRequest);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading file to S3: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Download file from S3 bucket
    /// </summary>
    public async Task<Stream?> DownloadFileAsync(string bucketName, string key)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request);
            return response.ResponseStream;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading file from S3: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// List objects in S3 bucket
    /// </summary>
    public async Task<ListObjectsV2Response?> ListObjectsAsync(string bucketName, string? prefix = null)
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = prefix
            };

            return await _s3Client.ListObjectsV2Async(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing S3 objects: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Delete file from S3 bucket
    /// </summary>
    public async Task<bool> DeleteFileAsync(string bucketName, string key)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting file from S3: {ex.Message}");
            return false;
        }
    }
}

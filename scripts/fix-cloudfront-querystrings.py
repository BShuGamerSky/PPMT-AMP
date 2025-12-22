#!/usr/bin/env python3
import boto3
import json

cf = boto3.client('cloudfront')
dist_id = 'E24T0TIWZPZ6C'

# Get current config
response = cf.get_distribution_config(Id=dist_id)
config = response['DistributionConfig']
etag = response['ETag']

# Remove QueryStringCacheKeys to forward ALL query parameters
if 'QueryStringCacheKeys' in config['DefaultCacheBehavior']['ForwardedValues']:
    del config['DefaultCacheBehavior']['ForwardedValues']['QueryStringCacheKeys']
    print("Removed QueryStringCacheKeys - will now forward ALL query parameters")

# Update distribution
cf.update_distribution(
    Id=dist_id,
    DistributionConfig=config,
    IfMatch=etag
)

print(f"✅ CloudFront {dist_id} updated - now forwards all query parameters")

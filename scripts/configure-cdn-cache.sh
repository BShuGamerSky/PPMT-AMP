#!/bin/bash
# Configure CloudFront cache policy after deployment completes
# This enables effective caching by excluding auth parameters from cache key

set -e

DISTRIBUTION_ID="E24OK55UTVZ2E3"
REGION="us-east-1"

echo "=========================================="
echo "CloudFront Cache Policy Configuration"
echo "=========================================="
echo ""

# Step 1: Check deployment status
echo "1. Checking CloudFront deployment status..."
STATUS=$(aws cloudfront get-distribution --id "$DISTRIBUTION_ID" --query 'Distribution.Status' --output text)
echo "   Status: $STATUS"
echo ""

if [ "$STATUS" != "Deployed" ]; then
    echo "⚠️  CloudFront is still deploying (Status: $STATUS)"
    echo "   Wait for deployment to complete before configuring cache policy."
    echo "   Check again with: aws cloudfront get-distribution --id $DISTRIBUTION_ID --query 'Distribution.Status' --output text"
    echo ""
    exit 1
fi

echo "✅ CloudFront is deployed and ready!"
echo ""

# Step 2: Create custom cache policy
echo "2. Creating custom cache policy (excludes auth parameters)..."

cat > /tmp/ppmt-cache-policy.json << 'EOF'
{
  "Name": "PPMT-AMP-API-CachePolicy",
  "Comment": "Cache by business params only, ignore auth params (timestamp, signature, appId, deviceId)",
  "DefaultTTL": 120,
  "MaxTTL": 300,
  "MinTTL": 60,
  "ParametersInCacheKeyAndForwardedToOrigin": {
    "EnableAcceptEncodingGzip": true,
    "EnableAcceptEncodingBrotli": true,
    "QueryStringsConfig": {
      "QueryStringBehavior": "whitelist",
      "QueryStrings": {
        "Quantity": 5,
        "Items": [
          "productId",
          "category",
          "startDate",
          "endDate",
          "limit"
        ]
      }
    },
    "HeadersConfig": {
      "HeaderBehavior": "none"
    },
    "CookiesConfig": {
      "CookieBehavior": "none"
    }
  }
}
EOF

POLICY_ID=$(aws cloudfront create-cache-policy \
  --cache-policy-config file:///tmp/ppmt-cache-policy.json \
  --query 'CachePolicy.Id' \
  --output text 2>&1)

if [[ $POLICY_ID == E* ]]; then
    echo "   ✓ Cache policy created: $POLICY_ID"
else
    echo "   ℹ️  Cache policy might already exist, trying to find it..."
    POLICY_ID=$(aws cloudfront list-cache-policies \
      --query "CachePolicyList.Items[?CachePolicy.CachePolicyConfig.Name=='PPMT-AMP-API-CachePolicy'].CachePolicy.Id" \
      --output text)
    echo "   ✓ Using existing policy: $POLICY_ID"
fi
echo ""

# Step 3: Get current distribution config
echo "3. Fetching current distribution configuration..."
aws cloudfront get-distribution-config --id "$DISTRIBUTION_ID" > /tmp/dist-config.json

ETAG=$(jq -r '.ETag' /tmp/dist-config.json)
echo "   ETag: $ETAG"
echo ""

# Step 4: Update distribution with cache policy
echo "4. Updating distribution to use custom cache policy..."

# Extract and modify config
jq --arg policy_id "$POLICY_ID" \
   '.DistributionConfig.DefaultCacheBehavior.CachePolicyId = $policy_id | 
    del(.DistributionConfig.DefaultCacheBehavior.ForwardedValues) | 
    del(.DistributionConfig.DefaultCacheBehavior.MinTTL) | 
    del(.DistributionConfig.DefaultCacheBehavior.DefaultTTL) | 
    del(.DistributionConfig.DefaultCacheBehavior.MaxTTL) | 
    .DistributionConfig' /tmp/dist-config.json > /tmp/new-config.json

aws cloudfront update-distribution \
  --id "$DISTRIBUTION_ID" \
  --distribution-config file:///tmp/new-config.json \
  --if-match "$ETAG" \
  --query 'Distribution.{Id:Id,Status:Status,DomainName:DomainName}' \
  --output json

echo ""
echo "=========================================="
echo "Configuration Complete! ✅"
echo "=========================================="
echo ""
echo "Cache Policy Applied:"
echo "  - Cache Key: productId, category, startDate, endDate, limit"
echo "  - Ignored: appId, deviceId, timestamp, signature"
echo "  - TTL: 60-300 seconds (default 120s)"
echo ""
echo "How it works:"
echo "  Request 1: /prices?productId=PROD-001&timestamp=123&signature=abc"
echo "  Request 2: /prices?productId=PROD-001&timestamp=456&signature=xyz"
echo "  → Same cache key! Request 2 = CACHE HIT ⚡"
echo ""
echo "Distribution will update in 2-3 minutes."
echo "Monitor: aws cloudfront get-distribution --id $DISTRIBUTION_ID --query 'Distribution.Status' --output text"
echo ""
echo "Test caching after update:"
echo "  python3 scripts/test-cdn-performance.py"
echo ""

# Cleanup
rm -f /tmp/ppmt-cache-policy.json /tmp/dist-config.json /tmp/new-config.json

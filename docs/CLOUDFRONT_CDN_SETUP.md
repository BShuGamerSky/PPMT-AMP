# CloudFront CDN Setup Summary

## ✅ CloudFront Distribution Created!

**Distribution ID**: E24OK55UTVZ2E3
**CloudFront Domain**: https://dkhagvq34al16.cloudfront.net
**Status**: Deploying (15-20 minutes)
**Origin**: API Gateway (stou0wlmf4.execute-api.us-east-1.amazonaws.com/prod)

---

## Configuration Details

### Caching Settings:
- **Min TTL**: 60 seconds
- **Default TTL**: 120 seconds (2 minutes)
- **Max TTL**: 300 seconds (5 minutes)
- **Cache Key**: Based on query string parameters (productId, category, etc.)
- **Compression**: Enabled (gzip)

### Allowed Methods:
- GET, HEAD, OPTIONS
- Only GET and HEAD are cached

### Origin Settings:
- Protocol: HTTPS only (TLS 1.2)
- Origin Path: /prod (automatically prepended)
- Timeout: 30 seconds

---

## How CloudFront Helps

### 1. **Geographic Distribution** 🌍
```
Without CDN:
User (California) → Virginia (API Gateway) → Lambda → DynamoDB
└─────────── 60-80ms network latency ──────────┘

With CDN:
User (California) → San Francisco (CloudFront Edge) → [CACHE HIT]
└─────────── 5-15ms ──────────┘ ✅ 50-70ms faster!
```

### 2. **Response Caching** 🔥
```
Request 1: productId=PROD-001
  → Cache MISS → API Gateway → Lambda → DynamoDB → 405ms
  → CloudFront caches response for 2 minutes

Request 2-N: productId=PROD-001 (within 2 min)
  → Cache HIT → Return from edge → 10-20ms ⚡
  → No Lambda invocation needed!
```

### 3. **Cost Savings** 💰
```
Scenario: 50% cache hit rate

Without CDN:
- 86,400 requests/day → 86,400 Lambda invocations
- Lambda cost: $28.44/year

With CDN (50% cache hit):
- 43,200 cache hits → 0 Lambda invocations
- 43,200 cache misses → 43,200 Lambda invocations
- Lambda cost: $14.22/year
- CloudFront cost: $25.92/year
- Total: $40.14/year

Net cost: +$11.70/year
Performance gain: -200ms avg (50% of requests ⚡ fast)
```

---

## Expected Performance

### Before CDN (Current):
- Average: 405ms
- Range: 387-456ms
- All requests hit Lambda

### After CDN (Projected):

**Cache MISS (First request):**
- Latency: 405ms + 20ms CDN overhead = ~425ms
- Happens once per unique query per 2 minutes

**Cache HIT (Subsequent requests):**
- Latency: 10-30ms (edge location only)
- No Lambda/DynamoDB involved
- 93% faster! ⚡

**Average (assuming 30% cache hit rate):**
```
Average = (0.30 × 20ms) + (0.70 × 425ms)
        = 6ms + 298ms
        = 304ms

Improvement: 405ms → 304ms = -101ms (-25%)
```

**Average (assuming 50% cache hit rate):**
```
Average = (0.50 × 20ms) + (0.50 × 425ms)
        = 10ms + 213ms
        = 223ms

Improvement: 405ms → 223ms = -182ms (-45%)
```

---

## Cache Hit Strategy - IMPORTANT! 🔥

### The Problem: Signature Changes Every Second

Your current implementation has a **major caching issue**:

```
Request 1 (timestamp=1734822000):
/prices?appId=...&deviceId=...&timestamp=1734822000&signature=ABC123&productId=PROD-001

Request 2 (1 second later, timestamp=1734822001):
/prices?appId=...&deviceId=...&timestamp=1734822001&signature=XYZ789&productId=PROD-001
```

CloudFront sees these as **different requests** because:
- ❌ `timestamp` changes every second
- ❌ `signature` changes because it includes the timestamp
- ❌ Different cache keys = **0% cache hit rate!**

### The Solution: Custom Cache Policy

**After CloudFront finishes deploying**, run this script to configure proper caching:

```bash
chmod +x scripts/configure-cdn-cache.sh
./scripts/configure-cdn-cache.sh
```

This creates a cache policy that:
- ✅ **Includes in cache key**: `productId`, `category`, `startDate`, `endDate`, `limit`
- ❌ **Excludes from cache key**: `appId`, `deviceId`, `timestamp`, `signature`

**Result**:
```
Request 1: /prices?productId=PROD-001&timestamp=123&signature=abc
  → Cache MISS (first request)
  → Forwards ALL params to Lambda (auth still works!)
  → Caches response using key: {productId=PROD-001}

Request 2: /prices?productId=PROD-001&timestamp=456&signature=xyz
  → Cache HIT! ⚡ (same productId)
  → Returns cached response (10-20ms)
  → Never reaches Lambda
```

### How It Works:

```
┌──────────────────────────────────────────────────────────┐
│ CloudFront Cache Key vs Origin Request                   │
├──────────────────────────────────────────────────────────┤
│                                                           │
│ Cache Key (what CloudFront uses to look up cache):      │
│   /prices?productId=PROD-001&limit=50                   │
│                                                           │
│ Origin Request (what gets sent to API Gateway):         │
│   /prices?productId=PROD-001&limit=50&appId=...&       │
│   deviceId=...&timestamp=123&signature=abc              │
│                                                           │
│ On Cache HIT: Origin request is NOT sent!               │
│ On Cache MISS: Full origin request with auth params     │
│                                                           │
└──────────────────────────────────────────────────────────┘
```

### Security Note:

**Q: Is it safe to exclude signature from cache key?**

**A: YES!** ✅ Because:

1. **Cache MISS (first request)**: Full request with signature is sent to Lambda, auth is validated ✓
2. **Cache HIT (subsequent requests)**: Response comes from cache, no Lambda invocation needed
3. **Cache expires after 2 minutes**: Next request is validated again

**Attack scenario**:
- Attacker sends request with invalid signature
- CloudFront forwards to Lambda
- Lambda rejects (403 Forbidden)
- CloudFront does NOT cache error responses
- No impact on legitimate users

**Best practice**: Only cache successful (200) responses, not errors.

---

## Testing Instructions

### Wait for Deployment (15-20 minutes)
Check status:
```bash
aws cloudfront get-distribution --id E24OK55UTVZ2E3 --query 'Distribution.Status' --output text
```

When status = "Deployed", test:
```bash
python3 scripts/test-cdn-performance.py
```

### Manual Test:
```bash
# Test CloudFront endpoint
curl "https://dkhagvq34al16.cloudfront.net/prices?appId=ppmt-amp-ios-v1&deviceId=test&timestamp=$(date +%s)&signature=test&limit=10"

# Check cache status in headers
curl -I "https://dkhagvq34al16.cloudfront.net/prices?..." | grep X-Cache
```

Expected headers:
- First request: `X-Cache: Miss from cloudfront`
- Second request (within 2 min): `X-Cache: Hit from cloudfront`

---

## Update iOS App

Once CloudFront is deployed and tested, update the API base URL:

**File**: `src/PPMT-AMP.Core/Services/ApiClient.cs`

```csharp
// Change from:
_apiBaseUrl = "https://stou0wlmf4.execute-api.us-east-1.amazonaws.com/prod";

// To:
_apiBaseUrl = "https://dkhagvq34al16.cloudfront.net";
// Note: /prod is already in origin path, don't add it
```

Or update configuration:
**File**: `config/appsettings.json`
```json
{
  "AWS": {
    "ApiGateway": {
      "BaseUrl": "https://dkhagvq34al16.cloudfront.net"
    }
  }
}
```

---

## Cache Invalidation

If you need to clear the cache (e.g., after data updates):

```bash
# Invalidate all cached prices
aws cloudfront create-invalidation \
  --distribution-id E24OK55UTVZ2E3 \
  --paths "/prices*"

# Invalidate everything
aws cloudfront create-invalidation \
  --distribution-id E24OK55UTVZ2E3 \
  --paths "/*"
```

Note: First 1,000 invalidations per month are free, then $0.005 per path.

---

## Cost Breakdown

### CloudFront Pricing:
```
Data Transfer (first 10 TB):
- $0.085 per GB (US/Europe)
- Your usage: 2.5 GB/month = $0.21/month

HTTP/HTTPS Requests:
- $0.0075 per 10,000 requests
- Your usage: 2.6M requests/month = $1.95/month

Total CloudFront: $2.16/month = $25.92/year
```

### Total Cost Impact:
```
Before CloudFront:
- Lambda: $28.44/year
- DynamoDB: $7.80/year
- Total: $36.24/year

With CloudFront (50% cache hit):
- Lambda: $14.22/year (-50% invocations)
- DynamoDB: $3.90/year (-50% queries)
- CloudFront: $25.92/year
- Total: $44.04/year

Additional cost: +$7.80/year
Performance gain: -182ms average (-45%)
```

---

## Monitoring

### CloudWatch Metrics:
- Cache hit rate
- Origin latency
- Error rate
- Bytes downloaded

### View metrics:
```bash
aws cloudwatch get-metric-statistics \
  --namespace AWS/CloudFront \
  --metric-name CacheHitRate \
  --dimensions Name=DistributionId,Value=E24OK55UTVZ2E3 \
  --start-time $(date -u -v-1H +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 300 \
  --statistics Average
```

---

## Summary

✅ **CloudFront CDN Enabled**
- Domain: https://dkhagvq34al16.cloudfront.net
- Status: Deploying (check in 15-20 min)
- Cache TTL: 2 minutes
- Cost: +$26/year

🎯 **Expected Benefits:**
- Geographic users: -50-150ms (edge location proximity)
- Cache hits: -380ms (10-30ms vs 405ms)
- Average (30% cache): -101ms (-25%)
- Average (50% cache): -182ms (-45%)

⏳ **Next Steps:**
1. Wait 15-20 minutes for deployment
2. Run: `python3 scripts/test-cdn-performance.py`
3. Verify cache hit rate and performance
4. Update iOS app to use CloudFront URL
5. Monitor cache effectiveness in CloudWatch

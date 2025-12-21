# PPMT-AMP Architecture & AWS Components

## ✅ Current Implementation Status

### What's Working Now:
```
iOS App → API Gateway → Lambda → DynamoDB
         (HTTPS)      (Proxy)   (Query)
```

**Authentication:** ✅ HMAC-SHA256 signature verification
**Data Access:** ✅ DynamoDB read operations
**Rate Limiting:** ✅ Client-side (20 req/5min)

---

## 1. Current API Gateway Usage

### Deployed Configuration
```
API Gateway: PPMT-AMP-API (stou0wlmf4)
Type: REST API (Regional)
Created: Dec 20, 2025
Stage: prod
```

### Current Endpoints
```
GET /prices
├── Integration: Lambda Proxy (ppmt-amp-price-query)
├── Authentication: Custom (HMAC signature)
├── Rate Limiting: Application-level
└── Response: JSON with price data
```

### Request Flow
```
1. iOS App generates HMAC signature
   ├── Message: "appId:deviceId:timestamp:GET:/prices"
   └── Secret: Shared secret key

2. API Gateway receives request
   ├── No API Gateway authentication (handled in Lambda)
   └── Forwards to Lambda via proxy integration

3. Lambda validates request
   ├── Verifies signature
   ├── Checks rate limit (DynamoDB)
   ├── Validates timestamp (5 min window)
   └── Queries DynamoDB if valid

4. DynamoDB returns data
   ├── Table: PPMT-AMP-Prices
   └── Scan operation (currently)

5. Lambda returns response
   └── API Gateway forwards to iOS app
```

### What API Gateway Does:
- ✅ HTTPS endpoint
- ✅ Request routing
- ✅ Lambda integration
- ❌ NO built-in auth (custom in Lambda)
- ❌ NO rate limiting (custom in app/Lambda)
- ❌ NO caching (could add)
- ❌ NO request validation (could add)

---

## 2. Complete AWS Architecture Plan

### Current Architecture (MVP - Implemented)
```
┌─────────────┐
│  iOS App    │
│  (Xamarin)  │
└──────┬──────┘
       │ HTTPS
       │ (HMAC signed)
       ▼
┌─────────────────────┐
│   API Gateway       │
│   (REST API)        │
│   /prices [GET]     │
└──────┬──────────────┘
       │ Proxy
       ▼
┌─────────────────────┐
│   Lambda            │
│   price_query       │
│   - Verify sig      │
│   - Rate limit      │
└──────┬──────────────┘
       │ Query
       ▼
┌─────────────────────┐
│   DynamoDB          │
│   PPMT-AMP-Prices   │
│   - ProductId (PK)  │
│   - PriceDate (SK)  │
└─────────────────────┘
```

### Phase 2: Data Upload (Next)
```
iOS App (Registered Users)
    │
    │ POST /prices/upload
    ▼
API Gateway
    │
    ▼
Lambda (upload_handler)
    ├─→ Validate user (Cognito)
    ├─→ Process CSV/JSON
    ├─→ Store raw in S3
    └─→ Write to DynamoDB
```

### Phase 3: Data Pipeline (Analytics)
```
DynamoDB Streams
    │ (Change events)
    ▼
Lambda (stream_processor)
    ├─→ Transform data
    ├─→ Export to S3
    └─→ Load to Redshift
         │
         ▼
    Redshift Warehouse
    - Historical analysis
    - Price trends
    - Market insights
```

### Phase 4: Offline Sync (Mobile)
```
iOS App
    │
    │ Background sync
    ▼
S3 Bucket (data-sync)
    ├─→ Daily snapshots
    ├─→ Incremental updates
    └─→ Offline cache
         │
         ▼
    Local SQLite
    - Offline queries
    - Fast UI
```

### Phase 5: Advanced Features
```
┌────────────────────────────────────┐
│         CloudWatch                 │
│  - Metrics, Logs, Alarms           │
└────────────────────────────────────┘
          │
          ▼
┌────────────────────────────────────┐
│      API Gateway (Enhanced)        │
│  - Usage Plans                     │
│  - API Keys (for partners)         │
│  - Request throttling              │
│  - Response caching                │
└────────────────────────────────────┘
          │
          ▼
┌────────────────────────────────────┐
│         Lambda Functions           │
│  - price_query (✅ Done)          │
│  - price_upload                    │
│  - price_export                    │
│  - analytics_query                 │
│  - notification_trigger            │
└────────────────────────────────────┘
          │
          ▼
┌────────────────────────────────────┐
│          Data Layer                │
│  - DynamoDB (✅ Real-time)        │
│  - S3 (Storage & Export)           │
│  - Redshift (Analytics)            │
│  - ElastiCache (Caching)           │
└────────────────────────────────────┘
```

---

## 3. Why S3 is in App Implementation?

### S3 Use Cases for iOS App

#### **Current Code (Prepared but Not Used Yet)**
```csharp
// S3Service.cs in PPMT-AMP.Core
public class S3Service {
    // Methods exist but not called yet:
    - UploadFileAsync()
    - DownloadFileAsync()
    - ListObjectsAsync()
}
```

#### **Why S3 Service Exists:**

### 1️⃣ **Future: Bulk Data Upload**
```csharp
// User uploads CSV file with 1000s of prices
var fileStream = File.OpenRead("prices.csv");
await s3Service.UploadFileAsync("raw/prices-2025-12-20.csv", fileStream);

// Lambda processes S3 file asynchronously
// → Parse CSV
// → Validate data
// → Bulk insert to DynamoDB
```

### 2️⃣ **Future: Offline Data Sync**
```csharp
// App downloads daily snapshot for offline use
await s3Service.DownloadFileAsync(
    "snapshots/prices-latest.json", 
    localPath
);

// Store in local SQLite for offline queries
```

### 3️⃣ **Future: Large Exports**
```csharp
// User exports price history (too large for API response)
await apiClient.RequestExportAsync(startDate, endDate);

// Lambda generates CSV → saves to S3
// App downloads from S3 directly
var exportUrl = await s3Service.GetPresignedUrlAsync("exports/user123-export.csv");
await DownloadFile(exportUrl);
```

### 4️⃣ **Future: Image/Document Upload**
```csharp
// User uploads product images
await s3Service.UploadFileAsync(
    "product-images/prod-123.jpg", 
    imageStream
);

// Save S3 URL in DynamoDB
await dynamoDbService.UpdateProductImage(productId, s3Url);
```

### **Why Include Now?**
- ✅ Architecture prepared for future features
- ✅ Service layer ready when needed
- ✅ No harm (just unused code)
- ✅ Team understands full architecture

---

## Complete Component Breakdown

### iOS App Components

```csharp
PPMT-AMP.iOS/
├── Views (UI)
│   ├── LoginViewController ✅ (Done)
│   └── MainViewController ✅ (Done)
│
└── PPMT-AMP.Core/
    ├── Services/
    │   ├── ApiClient ✅ (Used - API calls)
    │   ├── AuthService ✅ (Used - Anonymous/Access Keys)
    │   ├── AWSService ⏳ (Prepared - Not used yet)
    │   ├── S3Service ⏳ (Prepared - Not used yet)
    │   └── DynamoDBService ⏳ (Prepared - Not used yet)
    │
    ├── Models/
    │   ├── PriceData ✅ (Used)
    │   └── ApiModels ✅ (Used)
    │
    └── Configuration/
        └── AppConfiguration ✅ (Used)
```

**Why Unused Services Exist:**
- **S3Service**: For future file uploads/downloads
- **DynamoDBService**: For future direct DynamoDB access (if needed)
- **AWSService**: For future AWS SDK operations

**Current Flow Uses:**
```
App → ApiClient → API Gateway → Lambda → DynamoDB
     (HTTP)                    (SDK)
```

**Future Direct Access (Optional):**
```
App → S3Service → S3 Bucket
     (AWS SDK)

App → DynamoDBService → DynamoDB
     (AWS SDK)
```

---

## AWS Component Structure

### DynamoDB Tables
```
1. PPMT-AMP-Prices ✅ (Active)
   ├── Primary Key: Id (String)
   ├── GSI: DateIndex (PriceDate)
   ├── GSI: ProductIndex (ProductId)
   └── Attributes:
       ├── ProductName, ProductId
       ├── MarketPrice, RetailPrice
       ├── Currency, PriceDate
       ├── Category, Source, Status
       └── CreatedAt, UpdatedAt

2. PPMT-AMP-RateLimits ✅ (Active)
   ├── Primary Key: deviceId (String)
   ├── Attributes:
   │   ├── requestCount (Number)
   │   ├── windowStart (Number - Unix timestamp)
   │   └── lastRequest (Number - Unix timestamp)
   └── TTL: Auto-expire after 24 hours

3. PPMT-AMP-Users ⏳ (Future - for Cognito)
   └── User preferences, saved queries
```

### S3 Buckets
```
1. ppmt-amp-data-sync-363416481362 ✅ (Created)
   ├── raw/ (user uploads)
   ├── processed/ (validated data)
   └── archived/ (historical backups)

2. ppmt-amp-exports-363416481362 ✅ (Created)
   └── user-exports/ (generated reports)
```

### Lambda Functions
```
1. ppmt-amp-price-query ✅ (Deployed)
   ├── Trigger: API Gateway GET /prices
   ├── Function: Query prices with signature verification
   └── Permissions: DynamoDB read, Rate limit table R/W

2. ppmt-amp-upload-handler ⏳ (Future)
   ├── Trigger: API Gateway POST /prices/upload
   ├── Function: Process bulk price uploads
   └── Permissions: S3 write, DynamoDB write

3. ppmt-amp-data-export ⏳ (Future)
   ├── Trigger: API Gateway POST /export
   ├── Function: Generate CSV exports
   └── Permissions: DynamoDB read, S3 write

4. ppmt-amp-stream-processor ⏳ (Future)
   ├── Trigger: DynamoDB Streams
   ├── Function: Real-time data transformations
   └── Permissions: DynamoDB Streams, S3, Redshift
```

### API Gateway Structure
```
PPMT-AMP-API (stou0wlmf4)
├── Stage: prod
├── Endpoints:
│   ├── GET /prices ✅ (Active)
│   │   └── → Lambda: ppmt-amp-price-query
│   │
│   ├── POST /prices/upload ⏳ (Future)
│   │   └── → Lambda: ppmt-amp-upload-handler
│   │
│   ├── POST /export ⏳ (Future)
│   │   └── → Lambda: ppmt-amp-data-export
│   │
│   └── GET /analytics ⏳ (Future)
│       └── → Lambda: ppmt-amp-analytics
│
└── Features to Add:
    ├── Usage Plans (API quotas)
    ├── API Keys (partner access)
    ├── Request Validation
    ├── Response Caching
    └── CORS configuration
```

---

## Summary

### What's Actually Used Now:
```
iOS App
  ├─ ApiClient ✅
  ├─ AuthService ✅
  ├─ AppConfiguration ✅
  └─ Models ✅

AWS Active Resources:
  ├─ API Gateway (GET /prices) ✅
  ├─ Lambda (price_query_handler) ✅
  ├─ DynamoDB (PPMT-AMP-Prices) ✅
  └─ DynamoDB (PPMT-AMP-RateLimits) ✅
```

### What's Prepared But Not Used:
```
iOS App:
  ├─ S3Service ⏳ (for future uploads/downloads)
  ├─ DynamoDBService ⏳ (for future direct access)
  └─ AWSService ⏳ (for AWS SDK initialization)

AWS Resources:
  ├─ S3 Buckets ⏳ (created but empty)
  └─ Lambda functions ⏳ (not deployed yet)
```

### Why This Architecture?
1. **Scalable**: Each component can scale independently
2. **Modular**: Add features without breaking existing
3. **Cost-effective**: Pay only for what you use
4. **Future-proof**: Ready for offline sync, analytics, bulk uploads
5. **Development-friendly**: Services prepared but not forced into use yet

**Next Steps:** Keep developing with current simple flow. Add S3/advanced features only when actually needed!

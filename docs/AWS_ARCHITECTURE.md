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
Daily Data Scraping Pipeline:
┌─────────────────────────────────────────────────────────┐
│  External Data Sources (Market APIs, Websites)          │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────┐
│  Lambda (data_scraper)                                   │
│  - Schedule: CloudWatch Events (daily cron)              │
│  - Scrapes market data from multiple sources             │
│  - Raw data extraction                                   │
└──────────────────────┬──────────────────────────────────┘
                       │ Upload raw files
                       ▼
┌─────────────────────────────────────────────────────────┐
│  S3 Bucket (ppmt-amp-data-sync)                         │
│  /raw/YYYY-MM-DD/source1.csv                            │
│  /raw/YYYY-MM-DD/source2.json                           │
│  /raw/YYYY-MM-DD/source3.xml                            │
└──────────────────────┬──────────────────────────────────┘
                       │ Multiple sources
                       ▼
┌─────────────────────────────────────────────────────────┐
│  Redshift Data Warehouse                                 │
│  - COPY command from S3                                  │
│  - Staging tables per source                             │
│  - Daily ETL job (SQL stored procedures)                 │
│    ├── Data validation                                   │
│    ├── Deduplication                                     │
│    ├── Price normalization                               │
│    ├── Currency conversion                               │
│    └── Aggregation & enrichment                          │
└──────────────────────┬──────────────────────────────────┘
                       │ Processed data
                       ▼
┌─────────────────────────────────────────────────────────┐
│  Lambda (redshift_to_dynamodb_sync)                     │
│  - Triggered: After Redshift job completion              │
│  - Queries Redshift for processed records                │
│  - Batch writes to DynamoDB (25 items/request)           │
└──────────────────────┬──────────────────────────────────┘
                       │ Final data
                       ▼
┌─────────────────────────────────────────────────────────┐
│  DynamoDB (PPMT-AMP-Prices)                             │
│  - Real-time queries for iOS app                         │
│  - Latest validated prices                               │
└─────────────────────────────────────────────────────────┘
```

**Data Flow Summary:**
```
External APIs → Lambda Scraper → S3 (raw) → Redshift (ETL) → Lambda Sync → DynamoDB → iOS App
   (hourly)     (extract)      (staging)   (transform)      (load)       (real-time)
```

### User Upload (Optional Feature)
```
iOS App (Registered Users)
    │
    │ POST /prices/upload (manual upload)
    ▼
API Gateway
    │
    ▼
Lambda (user_upload_handler)
    ├─→ Store in S3 /user-uploads/
    └─→ Add to Redshift processing queue
         (processed in next daily ETL job)
```

### Phase 3: Superuser Management Portal (iOS)
```
iOS App (Superuser Role)
    │
    │ POST /prices/update
    │ POST /prices/create
    │ DELETE /prices/{id}
    ▼
API Gateway
    │ Verify superuser credentials
    ▼
Lambda (price_management_handler)
    ├─→ Validate superuser role (Cognito)
    ├─→ Validate price data
    ├─→ Direct DynamoDB write/update/delete
    └─→ Log audit trail
         │
         ▼
┌─────────────────────────────────────┐
│  DynamoDB (PPMT-AMP-Prices)         │
│  - Manual price updates             │
│  - Corrections and overrides        │
│  - Real-time changes                │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  DynamoDB (PPMT-AMP-AuditLog)       │
│  - Track who changed what           │
│  - Timestamp all modifications      │
│  - Superuser activity log           │
└─────────────────────────────────────┘
```

**Superuser Capabilities:**
- ✏️ Edit existing prices (market price, retail price)
- ➕ Add new products manually
- 🗑️ Delete incorrect/outdated entries
- 🔍 View audit logs of changes
- 🚫 Rate limiting exempt

**UI Features:**
```
MainViewController (Superuser Mode)
├── Query Prices (same as visitor)
├── ➕ Add New Price Button
├── Edit Mode (tap row to edit)
├── Delete Confirmation
└── Audit Log Viewer
```

### Phase 4: Advanced Analytics & Monitoring
```
CloudWatch Dashboards
├── API Request Metrics
├── Lambda Performance
├── DynamoDB Usage
├── Error Rates
└── Cost Tracking

SNS Notifications
├── Price anomaly alerts
├── ETL job failures
├── High error rates
└── Cost threshold alerts
```

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
    │   ├── AuthService ✅ (Used - Anonymous/Cognito)
    │   ├── S3Service ❌ (Remove - Not needed)
    │   ├── DynamoDBService ❌ (Remove - Not needed)
    │   └── AWSService ❌ (Remove - Not needed)
    │
    ├── Models/
    │   ├── PriceData ✅ (Used)
    │   └── ApiModels ✅ (Used)
    │
    └── Configuration/
        └── AppConfiguration ✅ (Used)
```

**Services to Remove:**
- **S3Service** ❌ - App should never access S3
- **DynamoDBService** ❌ - App should never directly access DynamoDB
- **AWSService** ❌ - No AWS SDK access needed from app

**Correct Pattern:**
```
App → ApiClient → API Gateway → Lambda → AWS Resources
     (HTTP only)              (AWS SDK)
```

**Why This is Better:**
1. ✅ **Security** - No AWS credentials in app
2. ✅ **Simplicity** - Single interface (ApiClient)
3. ✅ **Backend control** - All logic in Lambda
4. ✅ **Smaller app** - Fewer dependencies
5. ✅ **Easier testing** - Mock ApiClient only

**Current Flow Uses:**
```
App → ApiClient → API Gateway → Lambda → DynamoDB
     (HTTP + HMAC)           (AWS SDK)
```

**No Direct AWS Access Needed:**
```
❌ App → S3Service → S3 (WRONG - Security risk)
❌ App → DynamoDBService → DynamoDB (WRONG - No credentials)

✅ App → ApiClient → API → Lambda → AWS (CORRECT)
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

3. PPMT-AMP-Users ⏳ (Phase 3 - For Cognito)
   ├── Primary Key: userId (String)
   ├── Attributes:
   │   ├── email, username
   │   ├── role (visitor/user/superuser)
   │   ├── createdAt, lastLogin
   │   └── preferences
   └── GSI: RoleIndex (role)

4. PPMT-AMP-AuditLog ⏳ (Phase 3 - For superuser tracking)
   ├── Primary Key: logId (String)
   ├── Sort Key: timestamp (Number)
   ├── Attributes:
   │   ├── userId, action (create/update/delete)
   │   ├── priceId, oldValue, newValue
   │   └── ipAddress, userAgent
   └── TTL: Auto-expire after 90 days
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

2. ppmt-amp-data-scraper ⏳ (Phase 2 - Priority)
   ├── Trigger: CloudWatch Events (daily cron)
   ├── Function: Scrape market data from external APIs
   └── Permissions: S3 write (raw/)

3. ppmt-amp-redshift-sync ⏳ (Phase 2 - Priority)
   ├── Trigger: Manual/Scheduled after Redshift ETL
   ├── Function: Batch load processed data to DynamoDB
   └── Permissions: Redshift query, DynamoDB batch write

4. ppmt-amp-price-management ⏳ (Phase 3 - Superuser)
   ├── Trigger: API Gateway POST /prices/{create|update|delete}
   ├── Function: Handle superuser CRUD operations
   └── Permissions: Cognito verify, DynamoDB R/W, AuditLog write

5. ppmt-amp-data-export ⏳ (Future)
   ├── Trigger: API Gateway POST /export
   ├── Function: Generate CSV exports from DynamoDB
   └── Permissions: DynamoDB read, S3 write
```

### Redshift Cluster (Phase 2 - Priority)
```
PPMT-AMP-Warehouse
├── Node Type: dc2.large (start small)
├── Nodes: 2 (for redundancy)
├── Tables:
│   ├── staging_source1 (raw CSV data)
│   ├── staging_source2 (raw JSON data)
│   ├── staging_source3 (raw XML data)
│   ├── dim_products (dimension table)
│   ├── dim_categories (dimension table)
│   └── fact_prices (fact table - final processed)
│
├── Stored Procedures:
│   ├── sp_load_raw_data() - COPY from S3
│   ├── sp_validate_data() - Check constraints
│   ├── sp_deduplicate() - Remove duplicates
│   ├── sp_normalize_prices() - Currency conversion
│   └── sp_export_to_sync() - Prepare for DynamoDB
│
└── Daily ETL Job:
    1. COPY raw data from S3
    2. Validate and clean
    3. Transform and enrich
    4. Load to fact_prices
    5. Trigger Lambda sync to DynamoDB
```

### API Gateway Structure
```
PPMT-AMP-API (stou0wlmf4)
├── Stage: prod
├── Endpoints:
│   ├── GET /prices ✅ (Active)
│   │   └── → Lambda: ppmt-amp-price-query
│   │
│   ├── POST /prices/create ⏳ (Phase 3 - Superuser)
│   │   └── → Lambda: ppmt-amp-price-management
│   │
│   ├── POST /prices/update ⏳ (Phase 3 - Superuser)
│   │   └── → Lambda: ppmt-amp-price-management
│   │
│   ├── DELETE /prices/{id} ⏳ (Phase 3 - Superuser)
│   │   └── → Lambda: ppmt-amp-price-management
│   │
│   └── GET /audit-logs ⏳ (Phase 3 - Superuser)
│       └── → Lambda: ppmt-amp-price-management
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
  ├─ ApiClient ✅ (Only interface to backend)
  ├─ AuthService ✅ (Cognito authentication)
  ├─ AppConfiguration ✅ (Settings)
  └─ Models ✅ (Data structures)

AWS Active Resources:
  ├─ API Gateway (GET /prices) ✅
  ├─ Lambda (price_query_handler) ✅
  ├─ DynamoDB (PPMT-AMP-Prices) ✅
  ├─ DynamoDB (PPMT-AMP-RateLimits) ✅
  └─ S3 Buckets ✅ (backend ETL only)
```

### What Should Be Removed:
```
iOS App (Unnecessary Services):
  ├─ S3Service ❌ (App should never access S3)
  ├─ DynamoDBService ❌ (App should never access DynamoDB directly)
  └─ AWSService ❌ (No direct AWS SDK needed)
```

### Why This Architecture?
1. **Secure**: No AWS credentials in app, all auth via API Gateway
2. **Simple**: Single interface (ApiClient) for all backend operations
3. **Scalable**: Lambda can scale, enforce rate limits, validate requests
4. **Backend-controlled**: All business logic in Lambda, not app
5. **Clean separation**: S3/Redshift for ETL, DynamoDB for app data, API Gateway as boundary

**Data Ingestion:**
- **Automated**: Scraper → S3 → Redshift → DynamoDB (backend only)
- **Manual**: Superuser → API → Lambda → DynamoDB (via app)

**Data Consumption:**
- **App queries**: API → Lambda → DynamoDB (read-only for visitors, CRUD for superusers)

---

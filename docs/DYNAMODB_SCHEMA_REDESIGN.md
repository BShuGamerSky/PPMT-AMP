# DynamoDB Schema Redesign for PopMart Products

## Overview
Redesigning the database structure to better support PopMart blind box products with two tables:
1. **PPMT-AMP-Items**: Individual blind box figures with after-market pricing
2. **PPMT-AMP-Series**: Series-level information (release dates, descriptions, included items)

## Table 1: PPMT-AMP-Items (formerly PPMT-AMP-Prices)

### Primary Keys
- **Hash Key (Partition Key)**: `SeriesId` (String)
  - Format: `SERIES-{IP}-{SeriesName}` (e.g., `SERIES-LABUBU-MONSTERS`, `SERIES-HIRONO-WINTER2024`)
  - Groups all individual figures from the same blind box series together
  - Efficient for querying all figures within a series

- **Range Key (Sort Key)**: `ProductId` (String)
  - Format: `PROD-{SeriesId}-{Number}` (e.g., `PROD-LABUBU-MONSTERS-001`)
  - Unique identifier for each individual figure within the series
  - Allows sorting/filtering within a series

### Attributes

#### Core Product Information
- `ProductName` (String) - Name of the individual figure (e.g., "Labubu Sitting with Soda", "Hirono Winter Cap")
- `IpCharacter` (String) - IP character name (e.g., "Labubu", "Hirono", "Molly", "Skullpanda")
- `SeriesName` (String) - Full series name (e.g., "Monsters", "Winter Collection 2024")
- `Category` (String) - Product category (e.g., "Blind Box", "Mini Figure", "Plush")

#### Pricing Information
- `RetailPrice` (Number) - Official retail price for the unopened blind box (e.g., 69.00 CNY or 9.99 USD)
- `AfterMarketPrice` (Number) - Current after-market price for this specific unboxed figure (e.g., 299.00 CNY)
- `Currency` (String) - Price currency (e.g., "CNY", "USD")
- `PriceChange` (Number) - Price change vs retail (calculated: AfterMarketPrice - RetailPrice)
- `PriceChangePercent` (Number) - Percentage change (calculated: (AfterMarketPrice - RetailPrice) / RetailPrice * 100)

#### Metadata
- `Timestamp` (String) - ISO 8601 timestamp when data was imported/updated (e.g., "2025-12-21T10:30:00Z")
- `Status` (String) - Product status (e.g., "Active", "Discontinued", "Pre-Order")
- `Rarity` (String) - Figure rarity level (e.g., "Common", "Rare", "Secret", "Chase")
- `SeriesSize` (Number) - Total number of figures in this series (e.g., 12)
- `TTL` (Number) - Unix timestamp for automatic item expiration (e.g., 1735689600)

#### Additional Fields
- `ImageUrl` (String) - Product image URL
- `Description` (String) - Product description
- `CreatedAt` (String) - When record was first created
- `UpdatedAt` (String) - When record was last updated

### Global Secondary Indexes (GSIs)

#### GSI 1: IpCharacter-Timestamp-Index
- **Hash Key**: `IpCharacter` (String)
- **Range Key**: `Timestamp` (String)
- **Projection**: ALL
- **Use Case**: Query all figures for a specific IP character (e.g., all Labubu products), sorted by data freshness
- **Example Query**: "Show me all Labubu figures with latest prices"

#### GSI 2: Category-AfterMarketPrice-Index
- **Hash Key**: `Category` (String)
- **Range Key**: `AfterMarketPrice` (Number)
- **Projection**: ALL
- **Use Case**: Query by category, sorted by after-market price (highest to lowest)
- **Example Query**: "Show me the most expensive blind box figures"

#### GSI 3: Timestamp-Index
- **Hash Key**: `Status` (String)
- **Range Key**: `Timestamp` (String)
- **Projection**: ALL
- **Use Case**: Get all active products sorted by data freshness (for dashboard/home page)
- **Example Query**: "Show me all active products with latest data"

## Schema Comparison

### Old Schema
```
Primary Key: ProductId (Hash)
Range Key: PriceDate (Sort)
Attributes: Product, Category, MarketPrice, RetailPrice, Currency, Status, CreatedAt, UpdatedAt
GSIs: DateIndex (PriceDate), ProductIndex (ProductId)
```

### New Schema
```
Primary Key: SeriesId (Hash)
Range Key: ProductId (Sort)
Attributes: ProductName, IpCharacter, SeriesName, Category, RetailPrice, AfterMarketPrice, Currency, 
           PriceChange, PriceChangePercent, Timestamp, Status, Rarity, SeriesSize, TTL, ImageUrl, 
           Description, CreatedAt, UpdatedAt
GSIs: IpCharacter-Timestamp-Index, Category-AfterMarketPrice-Index, Timestamp-Index
```

## Key Improvements

### 1. Better Query Patterns
- **Old**: Query by ProductId (no series grouping)
- **New**: Query by SeriesId to get all figures in a series, or by IpCharacter to get all products for an IP

### 2. Pricing Clarity
- **Old**: "MarketPrice" was ambiguous (blind box or unboxed figure?)
- **New**: "RetailPrice" (blind box) + "AfterMarketPrice" (unboxed figure) + calculated fields

### 3. Data Freshness
- **Old**: "PriceDate" (ambiguous - price date or data date?)
- **New**: "Timestamp" (clear - when data was imported/updated)

### 4. TTL Support
- **Old**: No automatic expiration
- **New**: TTL field for automatic cleanup of old data (e.g., expire after 90 days)

### 5. IP Character Organization
- **Old**: No IP character tracking
- **New**: IpCharacter field with GSI for efficient IP-based queries

### 6. Rarity Tracking
- **New**: Rarity field to indicate figure rarity (important for after-market pricing)

## Sample Data

### Example 1: Labubu Monsters Series - Common Figure
```json
{
  "SeriesId": "SERIES-LABUBU-MONSTERS",
  "ProductId": "PROD-LABUBU-MONSTERS-001",
  "ProductName": "Labubu Sitting with Soda",
  "IpCharacter": "Labubu",
  "SeriesName": "Monsters Series",
  "Category": "Blind Box",
  "RetailPrice": 69.00,
  "AfterMarketPrice": 89.00,
  "Currency": "CNY",
  "PriceChange": 20.00,
  "PriceChangePercent": 28.99,
  "Timestamp": "2025-12-21T10:30:00Z",
  "Status": "Active",
  "Rarity": "Common",
  "SeriesSize": 12,
  "TTL": 1743350400,
  "ImageUrl": "https://cdn.popmart.com/labubu-monsters-001.jpg",
  "Description": "Labubu sitting with a refreshing soda drink",
  "CreatedAt": "2025-12-20T18:00:00Z",
  "UpdatedAt": "2025-12-21T10:30:00Z"
}
```

### Example 2: Labubu Monsters Series - Secret Figure
```json
{
  "SeriesId": "SERIES-LABUBU-MONSTERS",
  "ProductId": "PROD-LABUBU-MONSTERS-SECRET",
  "ProductName": "Labubu Golden Monster (Secret)",
  "IpCharacter": "Labubu",
  "SeriesName": "Monsters Series",
  "Category": "Blind Box",
  "RetailPrice": 69.00,
  "AfterMarketPrice": 899.00,
  "Currency": "CNY",
  "PriceChange": 830.00,
  "PriceChangePercent": 1202.90,
  "Timestamp": "2025-12-21T10:30:00Z",
  "Status": "Active",
  "Rarity": "Secret",
  "SeriesSize": 12,
  "TTL": 1743350400,
  "ImageUrl": "https://cdn.popmart.com/labubu-monsters-secret.jpg",
  "Description": "Ultra rare golden Labubu - 1/144 chance",
  "CreatedAt": "2025-12-20T18:00:00Z",
  "UpdatedAt": "2025-12-21T10:30:00Z"
}
```

### Example 3: Hirono Winter Collection
```json
{
  "SeriesId": "SERIES-HIRONO-WINTER2024",
  "ProductId": "PROD-HIRONO-WINTER2024-003",
  "ProductName": "Hirono with Snowflakes",
  "IpCharacter": "Hirono",
  "SeriesName": "Winter Collection 2024",
  "Category": "Blind Box",
  "RetailPrice": 79.00,
  "AfterMarketPrice": 129.00,
  "Currency": "CNY",
  "PriceChange": 50.00,
  "PriceChangePercent": 63.29,
  "Timestamp": "2025-12-21T10:30:00Z",
  "Status": "Active",
  "Rarity": "Rare",
  "SeriesSize": 8,
  "TTL": 1743350400,
  "ImageUrl": "https://cdn.popmart.com/hirono-winter-003.jpg",
  "Description": "Hirono surrounded by winter snowflakes",
  "CreatedAt": "2025-12-21T08:00:00Z",
  "UpdatedAt": "2025-12-21T10:30:00Z"
}
```

## Query Examples

### Query 1: Get all figures in Labubu Monsters series
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Items',
    KeyConditionExpression='SeriesId = :seriesId',
    ExpressionAttributeValues={
        ':seriesId': {'S': 'SERIES-LABUBU-MONSTERS'}
    }
)
# Returns all 12 figures in the series, sorted by ProductId
```

### Query 2: Get all Labubu products (any series)
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Items',
    IndexName='IpCharacter-Timestamp-Index',
    KeyConditionExpression='IpCharacter = :ip',
    ExpressionAttributeValues={
        ':ip': {'S': 'Labubu'}
    },
    ScanIndexForward=False  # Latest data first
)
# Returns all Labubu figures across all series
```

### Query 3: Get most expensive blind box figures
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Items',
    IndexName='Category-AfterMarketPrice-Index',
    KeyConditionExpression='Category = :category',
    ExpressionAttributeValues={
        ':category': {'S': 'Blind Box'}
    },
    ScanIndexForward=False  # Highest price first
)
# Returns blind box figures sorted by after-market price
```

### Query 4: Get latest active products for homepage
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Items',
    IndexName='Timestamp-Index',
    KeyConditionExpression='Status = :status',
    ExpressionAttributeValues={
        ':status': {'S': 'Active'}
    },
    ScanIndexForward=False,  # Latest timestamp first
    Limit=20
)
# Returns 20 most recently updated active products
```

## Table 2: PPMT-AMP-Series

### Primary Keys
- **Hash Key (Partition Key)**: `SeriesId` (String)
  - Format: `SERIES-{IP}-{SeriesName}` (e.g., `SERIES-LABUBU-MONSTERS`)
  - Unique identifier for each series

### Attributes

#### Series Information
- `SeriesName` (String) - Full series name (e.g., "Monsters Series", "Winter Collection 2024")
- `IpCharacter` (String) - Primary IP character (e.g., "Labubu", "Hirono", "Molly")
- `RelatedIpCharacters` (List<String>) - Related IP characters appearing in this series
- `Category` (String) - Series category (e.g., "Blind Box", "Mini Figure", "Mega Collection")
- `Description` (String) - Series description and story

#### Release Information
- `ReleaseDate` (String) - ISO 8601 release date (e.g., "2024-10-15")
- `Status` (String) - Series status ("Pre-Order", "Active", "Sold Out", "Discontinued")
- `Manufacturer` (String) - Manufacturer name (default: "Pop Mart")
- `Region` (String) - Primary release region ("CN", "US", "EU", "GLOBAL")

#### Series Details
- `TotalItems` (Number) - Total figures in series including secret (e.g., 12)
- `RegularItems` (Number) - Regular figures (e.g., 11)
- `SecretItems` (Number) - Secret/hidden figures (e.g., 1)
- `IncludedItems` (List<String>) - List of ProductIds in this series
- `PotentialUnboxedItems` (List<Map>) - List of possible figures with rarity
  ```json
  [
    {"productId": "PROD-001", "productName": "Labubu Sitting", "rarity": "Common", "odds": "1/12"},
    {"productId": "PROD-SECRET", "productName": "Golden Labubu", "rarity": "Secret", "odds": "1/144"}
  ]
  ```

#### Pricing Information
- `RetailPrice` (Number) - Official blind box retail price
- `Currency` (String) - Price currency (default: "CNY")
- `AverageAfterMarketPrice` (Number) - Average after-market price across all figures
- `LowestAfterMarketPrice` (Number) - Lowest figure price in series
- `HighestAfterMarketPrice` (Number) - Highest figure price in series

#### Visual Assets
- `ImageUrl` (String) - Main series box image
- `ThumbnailUrl` (String) - Thumbnail for list views
- `BannerUrl` (String) - Banner image for series page
- `GalleryImages` (List<String>) - Additional product images
  ```json
  [
    "https://cdn.popmart.com/series/labubu-monsters-banner.jpg",
    "https://cdn.popmart.com/series/labubu-monsters-lineup.jpg"
  ]
  ```

#### Metadata
- `Timestamp` (String) - ISO 8601 timestamp when data was last updated
- `CreatedAt` (String) - When series record was created
- `UpdatedAt` (String) - When series record was last updated
- `TTL` (Number) - Unix timestamp for automatic expiration

### Global Secondary Indexes (GSIs)

#### GSI 1: IpCharacter-ReleaseDate-Index
- **Hash Key**: `IpCharacter` (String)
- **Range Key**: `ReleaseDate` (String)
- **Projection**: ALL
- **Use Case**: Query all series for a specific IP character, sorted by release date
- **Example Query**: "Show me all Labubu series, newest first"

#### GSI 2: Status-ReleaseDate-Index
- **Hash Key**: `Status` (String)
- **Range Key**: `ReleaseDate` (String)
- **Projection**: ALL
- **Use Case**: Query by series status (Active, Pre-Order, etc.), sorted by release date
- **Example Query**: "Show me all upcoming pre-order series"

### Sample Series Data

```json
{
  "SeriesId": "SERIES-LABUBU-MONSTERS",
  "SeriesName": "Monsters Series",
  "IpCharacter": "Labubu",
  "RelatedIpCharacters": ["Labubu"],
  "Category": "Blind Box",
  "Description": "Labubu explores the world of cute monsters in this exciting series featuring 12 unique designs including 1 ultra-rare secret figure.",
  "ReleaseDate": "2024-10-15",
  "Status": "Active",
  "Manufacturer": "Pop Mart",
  "Region": "GLOBAL",
  "TotalItems": 12,
  "RegularItems": 11,
  "SecretItems": 1,
  "IncludedItems": [
    "PROD-LABUBU-MONSTERS-001",
    "PROD-LABUBU-MONSTERS-002",
    "PROD-LABUBU-MONSTERS-SECRET"
  ],
  "PotentialUnboxedItems": [
    {
      "productId": "PROD-LABUBU-MONSTERS-001",
      "productName": "Labubu Sitting with Soda",
      "rarity": "Common",
      "odds": "1/12"
    },
    {
      "productId": "PROD-LABUBU-MONSTERS-002",
      "productName": "Labubu Monster Hat",
      "rarity": "Common",
      "odds": "1/12"
    },
    {
      "productId": "PROD-LABUBU-MONSTERS-SECRET",
      "productName": "Labubu Golden Monster",
      "rarity": "Secret",
      "odds": "1/144"
    }
  ],
  "RetailPrice": 69.00,
  "Currency": "CNY",
  "AverageAfterMarketPrice": 189.50,
  "LowestAfterMarketPrice": 89.00,
  "HighestAfterMarketPrice": 899.00,
  "ImageUrl": "https://cdn.popmart.com/series/labubu-monsters-box.jpg",
  "ThumbnailUrl": "https://cdn.popmart.com/series/labubu-monsters-thumb.jpg",
  "BannerUrl": "https://cdn.popmart.com/series/labubu-monsters-banner.jpg",
  "GalleryImages": [
    "https://cdn.popmart.com/series/labubu-monsters-lineup.jpg",
    "https://cdn.popmart.com/series/labubu-monsters-display.jpg"
  ],
  "Timestamp": "2025-12-21T10:30:00Z",
  "CreatedAt": "2024-10-01T08:00:00Z",
  "UpdatedAt": "2025-12-21T10:30:00Z",
  "TTL": 1743350400
}
```

### Series Query Examples

#### Query 1: Get series details
```python
response = dynamodb.get_item(
    TableName='PPMT-AMP-Series',
    Key={'SeriesId': {'S': 'SERIES-LABUBU-MONSTERS'}}
)
# Returns complete series information
```

#### Query 2: Get all series for Labubu
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Series',
    IndexName='IpCharacter-ReleaseDate-Index',
    KeyConditionExpression='IpCharacter = :ip',
    ExpressionAttributeValues={
        ':ip': {'S': 'Labubu'}
    },
    ScanIndexForward=False  # Newest first
)
# Returns all Labubu series sorted by release date
```

#### Query 3: Get all active series
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Series',
    IndexName='Status-ReleaseDate-Index',
    KeyConditionExpression='Status = :status',
    ExpressionAttributeValues={
        ':status': {'S': 'Active'}
    },
    ScanIndexForward=False
)
# Returns all currently available series
```

#### Query 4: Get upcoming pre-orders
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Series',
    IndexName='Status-ReleaseDate-Index',
    KeyConditionExpression='Status = :status',
    ExpressionAttributeValues={
        ':status': {'S': 'Pre-Order'}
    },
    ScanIndexForward=True  # Earliest release first
)
# Returns upcoming series available for pre-order
```

## Query Examples

### Query 1: Get all figures in Labubu Monsters series
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Prices',
    KeyConditionExpression='SeriesId = :seriesId',
    ExpressionAttributeValues={
        ':seriesId': {'S': 'SERIES-LABUBU-MONSTERS'}
    }
)
# Returns all 12 figures in the series, sorted by ProductId
```

### Query 2: Get all Labubu products (any series)
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Prices',
    IndexName='IpCharacter-Timestamp-Index',
    KeyConditionExpression='IpCharacter = :ip',
    ExpressionAttributeValues={
        ':ip': {'S': 'Labubu'}
    },
    ScanIndexForward=False  # Latest data first
)
# Returns all Labubu figures across all series
```

### Query 3: Get most expensive blind box figures
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Prices',
    IndexName='Category-AfterMarketPrice-Index',
    KeyConditionExpression='Category = :category',
    ExpressionAttributeValues={
        ':category': {'S': 'Blind Box'}
    },
    ScanIndexForward=False  # Highest price first
)
# Returns blind box figures sorted by after-market price
```

### Query 4: Get latest active products for homepage
```python
response = dynamodb.query(
    TableName='PPMT-AMP-Prices',
    IndexName='Timestamp-Index',
    KeyConditionExpression='Status = :status',
    ExpressionAttributeValues={
        ':status': {'S': 'Active'}
    },
    ScanIndexForward=False,  # Latest timestamp first
    Limit=20
)
# Returns 20 most recently updated active products
```

## TTL Configuration

### TTL Field Setup
- **Attribute Name**: `TTL`
- **Data Type**: Number (Unix timestamp in seconds)
- **Calculation**: Current timestamp + retention period (e.g., 90 days)
- **Example**: For data imported on 2025-12-21, TTL = 1735689600 + (90 * 24 * 3600) = 1743465600

### TTL Benefits
1. **Automatic Cleanup**: DynamoDB automatically deletes items after TTL expires
2. **Cost Savings**: No need to pay for old data storage
3. **Data Freshness**: Ensures only recent after-market prices are available
4. **No Manual Maintenance**: No Lambda function needed for cleanup

### TTL Configuration Command
```bash
aws dynamodb update-time-to-live \
  --table-name PPMT-AMP-Prices \
  --time-to-live-specification "Enabled=true, AttributeName=TTL" \
  --region us-east-1
```

## Migration Strategy

### Phase 1: Backup Current Data
```bash
# Export current table to S3
aws dynamodb export-table-to-point-in-time \
  --table-arn arn:aws:dynamodb:us-east-1:363416481362:table/PPMT-AMP-Prices \
  --s3-bucket ppmt-amp-backups \
  --s3-prefix dynamodb-backup-2025-12-21 \
  --export-format DYNAMODB_JSON \
  --region us-east-1
```

### Phase 2: Create New Table Structure
```bash
# Delete old table (or rename to PPMT-AMP-Prices-Old)
aws dynamodb delete-table --table-name PPMT-AMP-Prices --region us-east-1

# Create new table with updated schema (see migration script)
```

### Phase 3: Transform and Load Data
```python
# Transform old data format to new schema
# Map: Product -> ProductName, MarketPrice -> AfterMarketPrice, PriceDate -> Timestamp
# Add: SeriesId, IpCharacter, SeriesName, Rarity, TTL
```

### Phase 4: Update Application Code
- Update Lambda handler to use new schema
- Update iOS models (PriceData.cs)
- Update API client query parameters
- Update test scripts

### Phase 5: Enable TTL
```bash
aws dynamodb update-time-to-live \
  --table-name PPMT-AMP-Prices \
  --time-to-live-specification "Enabled=true, AttributeName=TTL" \
  --region us-east-1
```

## Cost Impact

### Before (Current Schema)
- On-demand billing: $0.65/month
- 3 items, simple schema
- 2 GSIs (DateIndex, ProductIndex)

### After (New Schema)
- On-demand billing: $0.70-0.80/month (slightly higher due to more GSIs)
- Better query efficiency (less full table scans)
- 3 GSIs (IpCharacter-Timestamp-Index, Category-AfterMarketPrice-Index, Timestamp-Index)
- TTL enabled (free automatic cleanup)

**Net Impact**: +$0.05-0.15/month (~$0.60-1.80/year), but with significantly better query performance and automatic data cleanup.

## Next Steps

1. ✅ Review and approve new schema design
2. Create migration script with backup
3. Transform existing 3 products to new format
4. Update Lambda handler (price_query_handler.py)
5. Update iOS models (PriceData.cs, ApiModels.cs)
6. Update API client query logic
7. Update test scripts (test-api-query.py, analyze-performance.py)
8. Update documentation (API_SIGNATURE_STRATEGY.md, BACKEND_SUMMARY.md)
9. Test end-to-end with new schema
10. Deploy to production

## References
- DynamoDB TTL: https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/TTL.html
- GSI Best Practices: https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/bp-indexes-general.html
- PopMart Product Catalog: https://www.popmart.com/

# ETL Pipeline Architecture

## Overview

The PPMT-AMP data pipeline follows a robust ETL (Extract, Transform, Load) approach using AWS services. Data flows from external sources through S3 staging, Redshift processing, and finally to DynamoDB for real-time queries.

## Data Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         DAILY ETL PIPELINE                                │
└─────────────────────────────────────────────────────────────────────────┘

1. EXTRACT (Data Collection)
   ┌─────────────────────┐
   │  External Sources   │
   │  - Market APIs      │
   │  - Price websites   │
   │  - Partner feeds    │
   └──────────┬──────────┘
              │
              ▼
   ┌─────────────────────┐
   │  Lambda Scraper     │
   │  (data_scraper.py)  │
   │  - Scheduled daily  │
   │  - Multi-source     │
   │  - Error handling   │
   └──────────┬──────────┘
              │
              ▼
   ┌─────────────────────────────────────┐
   │  S3 Raw Data Lake                   │
   │  ppmt-amp-data-sync/raw/            │
   │  ├── 2025-12-21/                    │
   │  │   ├── ebay_prices.csv            │
   │  │   ├── amazon_prices.json         │
   │  │   ├── retailer_feed.xml          │
   │  │   └── metadata.json              │
   └──────────┬──────────────────────────┘
              │

2. TRANSFORM (Data Processing)
              │
              ▼
   ┌─────────────────────────────────────┐
   │  Redshift Data Warehouse            │
   │                                     │
   │  STAGE 1: Load Raw Data            │
   │  ├── staging_ebay                  │
   │  ├── staging_amazon                │
   │  └── staging_retailer              │
   │                                     │
   │  STAGE 2: Data Validation          │
   │  ├── Check required fields         │
   │  ├── Validate price ranges         │
   │  ├── Verify product IDs            │
   │  └── Flag anomalies                │
   │                                     │
   │  STAGE 3: Data Cleaning            │
   │  ├── Remove duplicates             │
   │  ├── Normalize product names       │
   │  ├── Standardize categories        │
   │  └── Convert currencies            │
   │                                     │
   │  STAGE 4: Data Enrichment          │
   │  ├── Calculate price trends        │
   │  ├── Add historical comparisons    │
   │  ├── Compute market averages       │
   │  └── Generate confidence scores    │
   │                                     │
   │  STAGE 5: Final Output             │
   │  └── fact_prices (ready for sync)  │
   └──────────┬──────────────────────────┘
              │

3. LOAD (Data Distribution)
              │
              ▼
   ┌─────────────────────────────────────┐
   │  Lambda Sync                        │
   │  (redshift_to_dynamodb_sync.py)     │
   │  - Query processed data             │
   │  - Batch writes (25 items)          │
   │  - Handle errors/retries            │
   └──────────┬──────────────────────────┘
              │
              ▼
   ┌─────────────────────────────────────┐
   │  DynamoDB                           │
   │  PPMT-AMP-Prices                    │
   │  - Real-time queries (<10ms)        │
   │  - iOS app data source              │
   │  - Latest validated prices          │
   └─────────────────────────────────────┘
```

---

## Phase 2 Implementation Details

### 1. Data Scraper Lambda

**Function:** `ppmt-amp-data-scraper`
**Runtime:** Python 3.11
**Schedule:** Daily at 2:00 AM UTC (CloudWatch Events)
**Timeout:** 15 minutes
**Memory:** 1024 MB

**Responsibilities:**
- Connect to external data sources (APIs, web scraping)
- Extract product pricing data
- Handle pagination and rate limits
- Error handling and retry logic
- Upload raw files to S3

**Output Format:**
```
s3://ppmt-amp-data-sync/raw/YYYY-MM-DD/
├── source_name_HHMMSS.csv
├── source_name_HHMMSS.json
├── metadata.json  (scraping stats, errors)
└── manifest.json  (files list, checksums)
```

**Environment Variables:**
```
S3_BUCKET=ppmt-amp-data-sync-363416481362
S3_PREFIX=raw
SOURCE_API_KEYS=<encrypted>
RETRY_ATTEMPTS=3
TIMEOUT_SECONDS=300
```

---

### 2. Redshift Data Warehouse

**Cluster:** `ppmt-amp-warehouse`
**Node Type:** `dc2.large` (160 GB SSD storage)
**Nodes:** 2 (Master + Compute)
**Database:** `ppmt_amp_db`

#### Schema Design

```sql
-- Staging Tables (Raw Data)
CREATE TABLE staging_source1 (
    raw_id VARCHAR(255),
    product_name VARCHAR(500),
    price DECIMAL(10,2),
    currency VARCHAR(10),
    source VARCHAR(100),
    scraped_date TIMESTAMP,
    raw_data VARCHAR(MAX)  -- Full JSON for debugging
);

CREATE TABLE staging_source2 (
    -- Similar structure
);

-- Dimension Tables
CREATE TABLE dim_products (
    product_id VARCHAR(100) PRIMARY KEY,
    product_name VARCHAR(500),
    normalized_name VARCHAR(500),
    category VARCHAR(100),
    subcategory VARCHAR(100),
    brand VARCHAR(200),
    created_at TIMESTAMP,
    updated_at TIMESTAMP
);

CREATE TABLE dim_categories (
    category_id VARCHAR(50) PRIMARY KEY,
    category_name VARCHAR(200),
    parent_category VARCHAR(200),
    display_order INT
);

-- Fact Table (Final Processed Data)
CREATE TABLE fact_prices (
    price_id VARCHAR(100) PRIMARY KEY,
    product_id VARCHAR(100) REFERENCES dim_products(product_id),
    market_price DECIMAL(10,2),
    retail_price DECIMAL(10,2),
    currency VARCHAR(10),
    price_date DATE,
    source VARCHAR(100),
    confidence_score DECIMAL(3,2),  -- 0.00 to 1.00
    trend_indicator VARCHAR(20),     -- 'rising', 'falling', 'stable'
    price_change_pct DECIMAL(5,2),   -- vs previous day
    processed_at TIMESTAMP,
    is_current BOOLEAN DEFAULT TRUE
);

-- Indexes for Performance
CREATE INDEX idx_fact_prices_date ON fact_prices(price_date);
CREATE INDEX idx_fact_prices_product ON fact_prices(product_id);
CREATE INDEX idx_fact_prices_current ON fact_prices(is_current);
```

#### ETL Stored Procedures

**1. Load Raw Data from S3**
```sql
CREATE OR REPLACE PROCEDURE sp_load_raw_data(p_date DATE)
AS $$
BEGIN
    -- Clear staging tables
    TRUNCATE TABLE staging_source1;
    TRUNCATE TABLE staging_source2;
    
    -- Load from S3
    COPY staging_source1
    FROM 's3://ppmt-amp-data-sync/raw/' || TO_CHAR(p_date, 'YYYY-MM-DD') || '/source1.csv'
    IAM_ROLE 'arn:aws:iam::363416481362:role/RedshiftS3ReadRole'
    CSV IGNOREHEADER 1
    DATEFORMAT 'auto'
    TIMEFORMAT 'auto';
    
    COPY staging_source2
    FROM 's3://ppmt-amp-data-sync/raw/' || TO_CHAR(p_date, 'YYYY-MM-DD') || '/source2.json'
    IAM_ROLE 'arn:aws:iam::363416481362:role/RedshiftS3ReadRole'
    JSON 'auto';
    
    COMMIT;
END;
$$ LANGUAGE plpgsql;
```

**2. Validate Data**
```sql
CREATE OR REPLACE PROCEDURE sp_validate_data()
AS $$
BEGIN
    -- Check for required fields
    DELETE FROM staging_source1 
    WHERE product_name IS NULL 
       OR price IS NULL 
       OR price <= 0 
       OR currency IS NULL;
    
    -- Check for price anomalies (e.g., price > $50,000)
    UPDATE staging_source1 
    SET raw_data = raw_data || '{"validation_flag":"price_anomaly"}'
    WHERE price > 50000;
    
    -- Log validation stats
    INSERT INTO etl_logs (job_date, stage, records_processed, records_rejected)
    SELECT CURRENT_DATE, 'validation', COUNT(*), 0
    FROM staging_source1;
    
    COMMIT;
END;
$$ LANGUAGE plpgsql;
```

**3. Deduplicate Data**
```sql
CREATE OR REPLACE PROCEDURE sp_deduplicate()
AS $$
BEGIN
    -- Remove exact duplicates
    DELETE FROM staging_source1
    WHERE raw_id IN (
        SELECT raw_id
        FROM (
            SELECT raw_id,
                   ROW_NUMBER() OVER (
                       PARTITION BY product_name, price, source
                       ORDER BY scraped_date DESC
                   ) AS rn
            FROM staging_source1
        ) WHERE rn > 1
    );
    
    COMMIT;
END;
$$ LANGUAGE plpgsql;
```

**4. Normalize and Transform**
```sql
CREATE OR REPLACE PROCEDURE sp_normalize_prices()
AS $$
BEGIN
    -- Convert all prices to USD
    UPDATE staging_source1
    SET price = CASE 
        WHEN currency = 'USD' THEN price
        WHEN currency = 'EUR' THEN price * 1.10  -- Example rate
        WHEN currency = 'GBP' THEN price * 1.28
        WHEN currency = 'JPY' THEN price * 0.0067
        ELSE price
    END,
    currency = 'USD';
    
    -- Normalize product names
    UPDATE staging_source1
    SET product_name = TRIM(UPPER(product_name)),
        product_name = REGEXP_REPLACE(product_name, '\s+', ' ');
    
    -- Assign categories based on keywords
    UPDATE staging_source1
    SET category = CASE
        WHEN product_name LIKE '%IPHONE%' THEN 'Smartphones'
        WHEN product_name LIKE '%MACBOOK%' THEN 'Laptops'
        WHEN product_name LIKE '%AIRPODS%' THEN 'Audio'
        ELSE 'Other'
    END;
    
    COMMIT;
END;
$$ LANGUAGE plpgsql;
```

**5. Load to Fact Table**
```sql
CREATE OR REPLACE PROCEDURE sp_load_fact_prices()
AS $$
BEGIN
    -- Mark previous records as not current
    UPDATE fact_prices
    SET is_current = FALSE
    WHERE is_current = TRUE
      AND price_date < CURRENT_DATE;
    
    -- Insert new processed records
    INSERT INTO fact_prices (
        price_id,
        product_id,
        market_price,
        retail_price,
        currency,
        price_date,
        source,
        confidence_score,
        processed_at,
        is_current
    )
    SELECT 
        MD5(product_name || TO_CHAR(CURRENT_DATE, 'YYYY-MM-DD') || source) AS price_id,
        COALESCE(dp.product_id, MD5(s.product_name)) AS product_id,
        AVG(s.price) AS market_price,
        MAX(s.price) AS retail_price,
        'USD' AS currency,
        CURRENT_DATE AS price_date,
        s.source,
        0.85 AS confidence_score,  -- Can be calculated based on data quality
        CURRENT_TIMESTAMP AS processed_at,
        TRUE AS is_current
    FROM staging_source1 s
    LEFT JOIN dim_products dp ON UPPER(TRIM(s.product_name)) = dp.normalized_name
    GROUP BY product_name, dp.product_id, s.source;
    
    COMMIT;
END;
$$ LANGUAGE plpgsql;
```

**6. Master ETL Job**
```sql
CREATE OR REPLACE PROCEDURE sp_daily_etl_job(p_date DATE)
AS $$
BEGIN
    -- Log job start
    INSERT INTO etl_logs (job_date, stage, status, start_time)
    VALUES (p_date, 'start', 'running', CURRENT_TIMESTAMP);
    
    -- Execute ETL steps
    CALL sp_load_raw_data(p_date);
    CALL sp_validate_data();
    CALL sp_deduplicate();
    CALL sp_normalize_prices();
    CALL sp_load_fact_prices();
    
    -- Log job completion
    INSERT INTO etl_logs (job_date, stage, status, end_time)
    VALUES (p_date, 'complete', 'success', CURRENT_TIMESTAMP);
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        INSERT INTO etl_logs (job_date, stage, status, error_message)
        VALUES (p_date, 'failed', 'error', SQLERRM);
        ROLLBACK;
        RAISE;
END;
$$ LANGUAGE plpgsql;
```

#### Scheduling the ETL Job

**Option 1: AWS Lambda Scheduler**
```python
# Lambda: ppmt-amp-etl-scheduler
import boto3
import psycopg2
from datetime import datetime

def lambda_handler(event, context):
    # Connect to Redshift
    conn = psycopg2.connect(
        host='ppmt-amp-warehouse.xxx.us-east-1.redshift.amazonaws.com',
        port=5439,
        database='ppmt_amp_db',
        user='admin',
        password=os.environ['REDSHIFT_PASSWORD']
    )
    
    cursor = conn.cursor()
    
    # Run ETL job
    today = datetime.now().date()
    cursor.execute(f"CALL sp_daily_etl_job('{today}')")
    
    conn.commit()
    cursor.close()
    conn.close()
    
    # Trigger sync Lambda after completion
    lambda_client = boto3.client('lambda')
    lambda_client.invoke(
        FunctionName='ppmt-amp-redshift-sync',
        InvocationType='Event'
    )
    
    return {'statusCode': 200, 'body': 'ETL job completed'}
```

**Option 2: CloudWatch Events Rule**
```json
{
  "schedule": "cron(0 4 * * ? *)",  // 4:00 AM UTC daily
  "target": {
    "arn": "arn:aws:lambda:us-east-1:363416481362:function:ppmt-amp-etl-scheduler"
  }
}
```

---

### 3. Redshift to DynamoDB Sync Lambda

**Function:** `ppmt-amp-redshift-sync`
**Runtime:** Python 3.11
**Trigger:** Invoked by ETL scheduler after Redshift job completes
**Timeout:** 15 minutes
**Memory:** 512 MB

**Implementation:**
```python
import boto3
import psycopg2
import os
from datetime import datetime

dynamodb = boto3.resource('dynamodb')
table = dynamodb.Table('PPMT-AMP-Prices')

def lambda_handler(event, context):
    # Connect to Redshift
    conn = psycopg2.connect(
        host=os.environ['REDSHIFT_HOST'],
        port=5439,
        database='ppmt_amp_db',
        user=os.environ['REDSHIFT_USER'],
        password=os.environ['REDSHIFT_PASSWORD']
    )
    
    cursor = conn.cursor()
    
    # Query latest processed prices
    query = """
        SELECT 
            price_id AS Id,
            product_id AS ProductId,
            p.product_name AS ProductName,
            market_price AS MarketPrice,
            retail_price AS RetailPrice,
            currency AS Currency,
            price_date AS PriceDate,
            source AS Source,
            confidence_score AS ConfidenceScore,
            'active' AS Status
        FROM fact_prices f
        JOIN dim_products p ON f.product_id = p.product_id
        WHERE is_current = TRUE
          AND processed_at >= CURRENT_DATE
    """
    
    cursor.execute(query)
    rows = cursor.fetchall()
    
    # Batch write to DynamoDB (25 items max per batch)
    batch_size = 25
    total_written = 0
    
    for i in range(0, len(rows), batch_size):
        batch = rows[i:i + batch_size]
        
        with table.batch_writer() as writer:
            for row in batch:
                item = {
                    'Id': row[0],
                    'ProductId': row[1],
                    'ProductName': row[2],
                    'MarketPrice': str(row[3]),
                    'RetailPrice': str(row[4]),
                    'Currency': row[5],
                    'PriceDate': str(row[6]),
                    'Source': row[7],
                    'ConfidenceScore': str(row[8]),
                    'Status': row[9],
                    'CreatedAt': datetime.utcnow().isoformat(),
                    'UpdatedAt': datetime.utcnow().isoformat()
                }
                writer.put_item(Item=item)
                total_written += 1
    
    cursor.close()
    conn.close()
    
    return {
        'statusCode': 200,
        'body': f'Synced {total_written} records to DynamoDB'
    }
```

**Environment Variables:**
```
REDSHIFT_HOST=ppmt-amp-warehouse.xxx.us-east-1.redshift.amazonaws.com
REDSHIFT_USER=admin
REDSHIFT_PASSWORD=<encrypted_via_SSM>
DYNAMODB_TABLE=PPMT-AMP-Prices
```

---

## Deployment Steps

### 1. Create Redshift Cluster
```bash
aws redshift create-cluster \
  --cluster-identifier ppmt-amp-warehouse \
  --node-type dc2.large \
  --number-of-nodes 2 \
  --master-username admin \
  --master-user-password <secure-password> \
  --cluster-subnet-group-name default \
  --publicly-accessible \
  --region us-east-1
```

### 2. Create Redshift IAM Role
```bash
aws iam create-role \
  --role-name RedshiftS3ReadRole \
  --assume-role-policy-document file://redshift-trust-policy.json

aws iam attach-role-policy \
  --role-name RedshiftS3ReadRole \
  --policy-arn arn:aws:iam::aws:policy/AmazonS3ReadOnlyAccess
```

### 3. Deploy Scraper Lambda
```bash
cd lambda
zip -r data_scraper.zip data_scraper.py requirements.txt

aws lambda create-function \
  --function-name ppmt-amp-data-scraper \
  --runtime python3.11 \
  --role arn:aws:iam::363416481362:role/LambdaExecutionRole \
  --handler data_scraper.lambda_handler \
  --zip-file fileb://data_scraper.zip \
  --timeout 900 \
  --memory-size 1024 \
  --environment Variables={S3_BUCKET=ppmt-amp-data-sync-363416481362}
```

### 4. Create CloudWatch Schedule
```bash
aws events put-rule \
  --name ppmt-amp-daily-scraper \
  --schedule-expression "cron(0 2 * * ? *)"

aws events put-targets \
  --rule ppmt-amp-daily-scraper \
  --targets "Id"="1","Arn"="arn:aws:lambda:us-east-1:363416481362:function:ppmt-amp-data-scraper"
```

### 5. Deploy Sync Lambda
```bash
cd lambda
zip -r redshift_sync.zip redshift_to_dynamodb_sync.py requirements.txt

aws lambda create-function \
  --function-name ppmt-amp-redshift-sync \
  --runtime python3.11 \
  --role arn:aws:iam::363416481362:role/LambdaRedshiftDynamoDBRole \
  --handler redshift_to_dynamodb_sync.lambda_handler \
  --zip-file fileb://redshift_sync.zip \
  --timeout 900 \
  --memory-size 512
```

---

## Monitoring and Logging

### CloudWatch Metrics
- **Lambda Scraper:**
  - Invocation count
  - Duration
  - Errors
  - Files uploaded to S3

- **Redshift ETL:**
  - Query execution time
  - Rows processed
  - Failed queries
  - Disk usage

- **Sync Lambda:**
  - Records synced
  - DynamoDB write throttles
  - Errors

### CloudWatch Alarms
```bash
# Alert if scraper fails
aws cloudwatch put-metric-alarm \
  --alarm-name ppmt-amp-scraper-failures \
  --alarm-description "Alert if scraper fails" \
  --metric-name Errors \
  --namespace AWS/Lambda \
  --statistic Sum \
  --period 300 \
  --threshold 1 \
  --comparison-operator GreaterThanThreshold

# Alert if ETL takes too long
aws cloudwatch put-metric-alarm \
  --alarm-name ppmt-amp-etl-duration \
  --alarm-description "Alert if ETL exceeds 30 minutes" \
  --metric-name Duration \
  --namespace AWS/Lambda \
  --statistic Average \
  --period 300 \
  --threshold 1800000 \
  --comparison-operator GreaterThanThreshold
```

---

## Cost Estimation (Phase 2)

### Monthly Costs
```
Redshift (dc2.large, 2 nodes):
  $0.25/hour × 2 nodes × 730 hours = ~$365/month

Lambda (data-scraper):
  Daily execution, ~5 min runtime
  512 MB memory
  ~$0.50/month

Lambda (redshift-sync):
  Daily execution, ~2 min runtime
  256 MB memory
  ~$0.20/month

S3 Storage:
  Raw data: ~10 GB/month
  $0.23/month

DynamoDB:
  On-demand pricing
  ~$5/month (current usage)

CloudWatch Logs:
  ~$2/month

Total: ~$373/month
```

**Cost Optimization:**
- Use Redshift pause/resume (pause when not processing)
- Archive old S3 data to Glacier
- Use reserved instances for Redshift if running 24/7

---

## Next Steps

1. ✅ Document ETL architecture (this file)
2. ⏳ Create Redshift cluster
3. ⏳ Implement scraper Lambda
4. ⏳ Set up Redshift schema and stored procedures
5. ⏳ Implement sync Lambda
6. ⏳ Test end-to-end pipeline
7. ⏳ Set up monitoring and alarms
8. ⏳ Schedule daily ETL jobs

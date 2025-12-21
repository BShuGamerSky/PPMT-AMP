# PPMT-AMP AWS Setup Guide
Complete setup guide for AWS infrastructure from scratch

## Phase 1: AWS Account Setup (5-10 minutes)

### Step 1: Create AWS Account
1. Go to https://aws.amazon.com
2. Click "Create an AWS Account"
3. Enter email, password, and account name
4. Provide payment information (required, but we'll use Free Tier)
5. Verify identity (phone verification)
6. Choose "Basic Support - Free" plan
7. Complete registration

### Step 2: Secure Your Account
```bash
# Enable MFA (Multi-Factor Authentication)
# 1. Go to IAM Dashboard
# 2. Click on your username → Security credentials
# 3. Enable MFA device (use Google Authenticator or similar)
```

### Step 3: Create IAM User for Development
```bash
# Don't use root account for development!
# 1. Go to IAM → Users → Add User
# 2. Username: ppmt-amp-dev
# 3. Enable: Access key - Programmatic access
# 4. Attach policies:
#    - AmazonDynamoDBFullAccess
#    - AmazonS3FullAccess
#    - AWSLambda_FullAccess
#    - AmazonAPIGatewayAdministrator
#    - AmazonRedshiftFullAccess
# 5. Save Access Key ID and Secret Access Key (you'll need these!)
```

## Phase 2: Infrastructure Setup

### Architecture Overview
```
iOS App
   ↓
API Gateway (REST API)
   ↓
Lambda Function (price_query_handler)
   ↓
├── DynamoDB (real-time data)
├── S3 (offline data sync & bulk storage)
└── Redshift (data warehouse for analytics)
```

### Step 1: Create DynamoDB Tables

```bash
# Install AWS CLI
brew install awscli

# Configure AWS CLI
aws configure
# Enter your Access Key ID
# Enter your Secret Access Key
# Region: us-east-1
# Output format: json

# Create Prices Table
aws dynamodb create-table \
    --table-name PPMT-AMP-Prices \
    --attribute-definitions \
        AttributeName=Id,AttributeType=S \
        AttributeName=PriceDate,AttributeType=S \
        AttributeName=ProductId,AttributeType=S \
    --key-schema \
        AttributeName=Id,KeyType=HASH \
    --global-secondary-indexes \
        "[
            {
                \"IndexName\":\"DateIndex\",
                \"KeySchema\":[{\"AttributeName\":\"PriceDate\",\"KeyType\":\"HASH\"}],
                \"Projection\":{\"ProjectionType\":\"ALL\"},
                \"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}
            },
            {
                \"IndexName\":\"ProductIndex\",
                \"KeySchema\":[{\"AttributeName\":\"ProductId\",\"KeyType\":\"HASH\"}],
                \"Projection\":{\"ProjectionType\":\"ALL\"},
                \"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}
            }
        ]" \
    --provisioned-throughput \
        ReadCapacityUnits=5,WriteCapacityUnits=5 \
    --tags Key=Project,Value=PPMT-AMP

# Create Rate Limits Table
aws dynamodb create-table \
    --table-name PPMT-AMP-RateLimits \
    --attribute-definitions \
        AttributeName=deviceId,AttributeType=S \
    --key-schema \
        AttributeName=deviceId,KeyType=HASH \
    --provisioned-throughput \
        ReadCapacityUnits=5,WriteCapacityUnits=5 \
    --tags Key=Project,Value=PPMT-AMP

# Verify tables created
aws dynamodb list-tables
```

### Step 2: Create S3 Buckets

```bash
# Create bucket for data sync
aws s3 mb s3://ppmt-amp-data-sync --region us-east-1

# Create bucket for bulk exports
aws s3 mb s3://ppmt-amp-exports --region us-east-1

# Enable versioning (optional but recommended)
aws s3api put-bucket-versioning \
    --bucket ppmt-amp-data-sync \
    --versioning-configuration Status=Enabled

# Set up bucket structure
aws s3api put-object --bucket ppmt-amp-data-sync --key raw/
aws s3api put-object --bucket ppmt-amp-data-sync --key processed/
aws s3api put-object --bucket ppmt-amp-data-sync --key archived/

# Configure lifecycle policy (auto-delete old files)
cat > lifecycle-policy.json << 'EOF'
{
    "Rules": [
        {
            "Id": "ArchiveOldData",
            "Status": "Enabled",
            "Prefix": "raw/",
            "Transitions": [
                {
                    "Days": 30,
                    "StorageClass": "GLACIER"
                }
            ],
            "Expiration": {
                "Days": 365
            }
        }
    ]
}
EOF

aws s3api put-bucket-lifecycle-configuration \
    --bucket ppmt-amp-data-sync \
    --lifecycle-configuration file://lifecycle-policy.json
```

### Step 3: Create Lambda Function

```bash
# Create execution role
aws iam create-role \
    --role-name ppmt-amp-lambda-role \
    --assume-role-policy-document '{
        "Version": "2012-10-17",
        "Statement": [{
            "Effect": "Allow",
            "Principal": {"Service": "lambda.amazonaws.com"},
            "Action": "sts:AssumeRole"
        }]
    }'

# Attach policies
aws iam attach-role-policy \
    --role-name ppmt-amp-lambda-role \
    --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole

# Create custom policy for DynamoDB and S3
cat > lambda-policy.json << 'EOF'
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "dynamodb:GetItem",
                "dynamodb:PutItem",
                "dynamodb:UpdateItem",
                "dynamodb:Scan",
                "dynamodb:Query"
            ],
            "Resource": [
                "arn:aws:dynamodb:*:*:table/PPMT-AMP-Prices",
                "arn:aws:dynamodb:*:*:table/PPMT-AMP-Prices/index/*",
                "arn:aws:dynamodb:*:*:table/PPMT-AMP-RateLimits"
            ]
        },
        {
            "Effect": "Allow",
            "Action": [
                "s3:GetObject",
                "s3:PutObject",
                "s3:ListBucket"
            ],
            "Resource": [
                "arn:aws:s3:::ppmt-amp-data-sync",
                "arn:aws:s3:::ppmt-amp-data-sync/*",
                "arn:aws:s3:::ppmt-amp-exports",
                "arn:aws:s3:::ppmt-amp-exports/*"
            ]
        }
    ]
}
EOF

aws iam put-role-policy \
    --role-name ppmt-amp-lambda-role \
    --policy-name ppmt-amp-access-policy \
    --policy-document file://lambda-policy.json

# Get role ARN (you'll need this)
ROLE_ARN=$(aws iam get-role --role-name ppmt-amp-lambda-role --query 'Role.Arn' --output text)
echo "Lambda Role ARN: $ROLE_ARN"

# Wait for role to propagate
sleep 10

# Package Lambda function
cd lambda
zip lambda_function.zip price_query_handler.py

# Create Lambda function
aws lambda create-function \
    --function-name ppmt-amp-price-query \
    --runtime python3.11 \
    --role $ROLE_ARN \
    --handler price_query_handler.lambda_handler \
    --zip-file fileb://lambda_function.zip \
    --timeout 30 \
    --memory-size 256 \
    --environment Variables="{APP_SECRET=your-secret-key-change-this-in-production}" \
    --description "PPMT-AMP price query API with rate limiting"

# Get Lambda ARN
LAMBDA_ARN=$(aws lambda get-function --function-name ppmt-amp-price-query --query 'Configuration.FunctionArn' --output text)
echo "Lambda ARN: $LAMBDA_ARN"
```

### Step 4: Create API Gateway

```bash
# Create REST API
API_ID=$(aws apigateway create-rest-api \
    --name "PPMT-AMP-API" \
    --description "API for PPMT-AMP price queries with security" \
    --endpoint-configuration types=REGIONAL \
    --query 'id' --output text)

echo "API ID: $API_ID"

# Get root resource ID
ROOT_ID=$(aws apigateway get-resources \
    --rest-api-id $API_ID \
    --query "items[?path=='/'].id" \
    --output text)

# Create /prices resource
PRICES_RESOURCE_ID=$(aws apigateway create-resource \
    --rest-api-id $API_ID \
    --parent-id $ROOT_ID \
    --path-part prices \
    --query 'id' --output text)

# Create GET method
aws apigateway put-method \
    --rest-api-id $API_ID \
    --resource-id $PRICES_RESOURCE_ID \
    --http-method GET \
    --authorization-type NONE \
    --no-api-key-required

# Get AWS region and account ID
REGION=$(aws configure get region)
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)

# Integrate with Lambda
aws apigateway put-integration \
    --rest-api-id $API_ID \
    --resource-id $PRICES_RESOURCE_ID \
    --http-method GET \
    --type AWS_PROXY \
    --integration-http-method POST \
    --uri "arn:aws:apigateway:${REGION}:lambda:path/2015-03-31/functions/${LAMBDA_ARN}/invocations"

# Set up method response
aws apigateway put-method-response \
    --rest-api-id $API_ID \
    --resource-id $PRICES_RESOURCE_ID \
    --http-method GET \
    --status-code 200 \
    --response-models '{"application/json": "Empty"}'

# Grant API Gateway permission to invoke Lambda
aws lambda add-permission \
    --function-name ppmt-amp-price-query \
    --statement-id apigateway-invoke-prices \
    --action lambda:InvokeFunction \
    --principal apigateway.amazonaws.com \
    --source-arn "arn:aws:execute-api:${REGION}:${ACCOUNT_ID}:${API_ID}/*"

# Deploy API
aws apigateway create-deployment \
    --rest-api-id $API_ID \
    --stage-name prod \
    --stage-description "Production stage" \
    --description "Initial deployment"

# Get API endpoint
API_ENDPOINT="https://${API_ID}.execute-api.${REGION}.amazonaws.com/prod"
echo "API Endpoint: $API_ENDPOINT"
echo "Test URL: ${API_ENDPOINT}/prices?appId=ppmt-amp-ios-v1&deviceId=test&timestamp=$(date +%s)&signature=test"
```

### Step 5: Set Up Redshift (Data Warehouse)

```bash
# Create Redshift subnet group
aws redshift create-cluster-subnet-group \
    --cluster-subnet-group-name ppmt-amp-subnet-group \
    --description "Subnet group for PPMT-AMP Redshift" \
    --subnet-ids subnet-xxxxx subnet-yyyyy  # Use your VPC subnet IDs

# Create Redshift cluster (dc2.large = cheapest option, ~$180/month or free trial)
aws redshift create-cluster \
    --cluster-identifier ppmt-amp-datawarehouse \
    --node-type dc2.large \
    --master-username admin \
    --master-user-password YourSecurePassword123! \
    --cluster-type single-node \
    --db-name ppmt_amp \
    --cluster-subnet-group-name ppmt-amp-subnet-group \
    --publicly-accessible \
    --port 5439

# Wait for cluster to be available (takes 5-10 minutes)
aws redshift describe-clusters \
    --cluster-identifier ppmt-amp-datawarehouse \
    --query 'Clusters[0].ClusterStatus'

# Get Redshift endpoint
REDSHIFT_ENDPOINT=$(aws redshift describe-clusters \
    --cluster-identifier ppmt-amp-datawarehouse \
    --query 'Clusters[0].Endpoint.Address' \
    --output text)

echo "Redshift Endpoint: $REDSHIFT_ENDPOINT"
```

### Step 6: Create Redshift Tables

```sql
-- Connect to Redshift using psql or DBeaver
-- Connection string: postgresql://admin:YourSecurePassword123!@REDSHIFT_ENDPOINT:5439/ppmt_amp

-- Create schema
CREATE SCHEMA ppmt_amp;

-- Create price data table
CREATE TABLE ppmt_amp.prices (
    id VARCHAR(100) PRIMARY KEY,
    product_id VARCHAR(50),
    product_name VARCHAR(255),
    market_price DECIMAL(10,2),
    retail_price DECIMAL(10,2),
    currency VARCHAR(10),
    price_date DATE,
    source VARCHAR(100),
    category VARCHAR(100),
    created_at TIMESTAMP,
    updated_at TIMESTAMP,
    status VARCHAR(50)
)
DISTSTYLE AUTO
SORTKEY (price_date);

-- Create aggregated daily prices table
CREATE TABLE ppmt_amp.daily_price_summary (
    price_date DATE,
    product_id VARCHAR(50),
    category VARCHAR(100),
    avg_market_price DECIMAL(10,2),
    avg_retail_price DECIMAL(10,2),
    min_price DECIMAL(10,2),
    max_price DECIMAL(10,2),
    record_count INTEGER,
    created_at TIMESTAMP,
    PRIMARY KEY (price_date, product_id)
)
DISTSTYLE AUTO
SORTKEY (price_date);
```

### Step 7: Set Up Data Pipeline (DynamoDB → S3 → Redshift)

```bash
# Create Lambda function for data export
cat > export_handler.py << 'EOF'
import boto3
import json
from datetime import datetime, timedelta

dynamodb = boto3.client('dynamodb')
s3 = boto3.client('s3')

def lambda_handler(event, context):
    """Export DynamoDB data to S3 for Redshift import"""
    
    # Scan DynamoDB table
    response = dynamodb.scan(TableName='PPMT-AMP-Prices')
    items = response['Items']
    
    # Convert to CSV format
    csv_lines = ['id,product_id,product_name,market_price,retail_price,currency,price_date,source,category,created_at,updated_at,status']
    
    for item in items:
        line = ','.join([
            item.get('Id', {}).get('S', ''),
            item.get('ProductId', {}).get('S', ''),
            item.get('ProductName', {}).get('S', ''),
            item.get('MarketPrice', {}).get('N', '0'),
            item.get('RetailPrice', {}).get('N', '0'),
            item.get('Currency', {}).get('S', 'USD'),
            item.get('PriceDate', {}).get('S', ''),
            item.get('Source', {}).get('S', ''),
            item.get('Category', {}).get('S', ''),
            item.get('CreatedAt', {}).get('S', ''),
            item.get('UpdatedAt', {}).get('S', ''),
            item.get('Status', {}).get('S', '')
        ])
        csv_lines.append(line)
    
    csv_content = '\n'.join(csv_lines)
    
    # Upload to S3
    date_str = datetime.utcnow().strftime('%Y-%m-%d')
    s3_key = f'exports/prices_{date_str}.csv'
    
    s3.put_object(
        Bucket='ppmt-amp-exports',
        Key=s3_key,
        Body=csv_content,
        ContentType='text/csv'
    )
    
    return {
        'statusCode': 200,
        'body': json.dumps(f'Exported {len(items)} records to s3://ppmt-amp-exports/{s3_key}')
    }
EOF

# Create export Lambda
zip export_function.zip export_handler.py

aws lambda create-function \
    --function-name ppmt-amp-data-export \
    --runtime python3.11 \
    --role $ROLE_ARN \
    --handler export_handler.lambda_handler \
    --zip-file fileb://export_function.zip \
    --timeout 300 \
    --memory-size 512

# Schedule daily export (runs at 2 AM UTC)
aws events put-rule \
    --name ppmt-amp-daily-export \
    --schedule-expression "cron(0 2 * * ? *)"

aws lambda add-permission \
    --function-name ppmt-amp-data-export \
    --statement-id allow-eventbridge \
    --action lambda:InvokeFunction \
    --principal events.amazonaws.com

aws events put-targets \
    --rule ppmt-amp-daily-export \
    --targets "Id"="1","Arn"="$LAMBDA_ARN"
```

### Step 8: Configure iOS App with AWS Credentials

Update `config/appsettings.json`:
```json
{
  "AWS": {
    "Region": "us-east-1",
    "S3": {
      "BucketName": "ppmt-amp-data-sync",
      "DataPath": "raw/"
    },
    "DynamoDB": {
      "TableName": "PPMT-AMP-Prices",
      "IndexName": "DateIndex"
    },
    "ApiGateway": {
      "BaseUrl": "https://YOUR_API_ID.execute-api.us-east-1.amazonaws.com/prod",
      "AppId": "ppmt-amp-ios-v1",
      "AppSecret": "your-secret-key-change-this-in-production"
    }
  }
}
```

## Phase 3: Testing

```bash
# Test DynamoDB
aws dynamodb put-item \
    --table-name PPMT-AMP-Prices \
    --item '{
        "Id": {"S": "test-001"},
        "ProductId": {"S": "PROD-001"},
        "ProductName": {"S": "Test Product"},
        "MarketPrice": {"N": "999.99"},
        "RetailPrice": {"N": "899.99"},
        "Currency": {"S": "USD"},
        "PriceDate": {"S": "2025-12-20"},
        "Category": {"S": "Electronics"},
        "Status": {"S": "Active"}
    }'

# Test S3
echo "test data" > test.txt
aws s3 cp test.txt s3://ppmt-amp-data-sync/raw/test.txt

# Test Lambda
aws lambda invoke \
    --function-name ppmt-amp-price-query \
    --payload '{"queryStringParameters":{"appId":"ppmt-amp-ios-v1","deviceId":"test","timestamp":"'$(date +%s)'","signature":"test","limit":"10"}}' \
    response.json

cat response.json

# Test API Gateway
curl "${API_ENDPOINT}/prices?appId=ppmt-amp-ios-v1&deviceId=test&timestamp=$(date +%s)&signature=test&limit=10"
```

## Cost Estimation (Monthly)

### Free Tier (First 12 months)
- DynamoDB: 25 GB storage + 200M requests/month - FREE
- Lambda: 1M requests + 400,000 GB-seconds - FREE
- S3: 5 GB storage + 20,000 GET requests - FREE
- API Gateway: 1M requests - FREE

### After Free Tier
- DynamoDB: ~$1.25/month (25 GB)
- Lambda: ~$0.20/month (light usage)
- S3: ~$0.10/month (10 GB)
- API Gateway: ~$3.50/month (1M requests)
- Redshift: ~$180/month (dc2.large) or pause when not in use
- **Total: ~$5/month (without Redshift)**
- **Total: ~$185/month (with Redshift)**

### Cost Optimization Tips
1. Use Redshift only when needed (pause cluster)
2. Enable S3 lifecycle policies for old data
3. Use DynamoDB on-demand pricing if sporadic usage
4. Set up billing alerts

## Next Steps
1. Run through Phase 1-2 to set up infrastructure
2. Update iOS app configuration with actual endpoints
3. Test with iOS simulator
4. Deploy to TestFlight for testing
5. Monitor costs and usage

## Troubleshooting
- If Lambda fails: Check CloudWatch Logs
- If API Gateway 403: Check signature and timestamp
- If DynamoDB throttling: Increase read/write capacity
- If S3 access denied: Check IAM role permissions

## Support
- AWS Documentation: https://docs.aws.amazon.com
- AWS Free Tier: https://aws.amazon.com/free
- AWS Cost Calculator: https://calculator.aws

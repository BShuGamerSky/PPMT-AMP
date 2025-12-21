#!/bin/bash
# PPMT-AMP AWS Infrastructure Setup Script
# Run this script after creating your AWS account and configuring AWS CLI

set -e  # Exit on error

echo "=========================================="
echo "PPMT-AMP AWS Infrastructure Setup"
echo "=========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if AWS CLI is installed
if ! command -v aws &> /dev/null; then
    echo -e "${RED}Error: AWS CLI is not installed${NC}"
    echo "Install it with: brew install awscli"
    exit 1
fi

# Check if AWS is configured
if ! aws sts get-caller-identity &> /dev/null; then
    echo -e "${RED}Error: AWS CLI is not configured${NC}"
    echo "Run: aws configure"
    exit 1
fi

# Get AWS account info
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
REGION=$(aws configure get region || echo "us-east-1")

echo -e "${GREEN}AWS Account ID: $ACCOUNT_ID${NC}"
echo -e "${GREEN}Region: $REGION${NC}"
echo ""

read -p "Continue with setup? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    exit 1
fi

echo ""
echo "Step 1: Creating DynamoDB Tables..."
echo "-----------------------------------"

# Create Prices table
aws dynamodb create-table \
    --table-name PPMT-AMP-Prices \
    --attribute-definitions \
        AttributeName=Id,AttributeType=S \
        AttributeName=PriceDate,AttributeType=S \
        AttributeName=ProductId,AttributeType=S \
    --key-schema \
        AttributeName=Id,KeyType=HASH \
    --global-secondary-indexes \
        "[{\"IndexName\":\"DateIndex\",\"KeySchema\":[{\"AttributeName\":\"PriceDate\",\"KeyType\":\"HASH\"}],\"Projection\":{\"ProjectionType\":\"ALL\"},\"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}},{\"IndexName\":\"ProductIndex\",\"KeySchema\":[{\"AttributeName\":\"ProductId\",\"KeyType\":\"HASH\"}],\"Projection\":{\"ProjectionType\":\"ALL\"},\"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}}]" \
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 \
    --region $REGION \
    2>/dev/null && echo -e "${GREEN}✓ PPMT-AMP-Prices table created${NC}" || echo -e "${YELLOW}Table may already exist${NC}"

# Create Rate Limits table
aws dynamodb create-table \
    --table-name PPMT-AMP-RateLimits \
    --attribute-definitions AttributeName=deviceId,AttributeType=S \
    --key-schema AttributeName=deviceId,KeyType=HASH \
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 \
    --region $REGION \
    2>/dev/null && echo -e "${GREEN}✓ PPMT-AMP-RateLimits table created${NC}" || echo -e "${YELLOW}Table may already exist${NC}"

echo ""
echo "Step 2: Creating S3 Buckets..."
echo "------------------------------"

# Create buckets
aws s3 mb s3://ppmt-amp-data-sync-${ACCOUNT_ID} --region $REGION 2>/dev/null && \
    echo -e "${GREEN}✓ Data sync bucket created${NC}" || echo -e "${YELLOW}Bucket may already exist${NC}"

aws s3 mb s3://ppmt-amp-exports-${ACCOUNT_ID} --region $REGION 2>/dev/null && \
    echo -e "${GREEN}✓ Exports bucket created${NC}" || echo -e "${YELLOW}Bucket may already exist${NC}"

# Create folder structure
aws s3api put-object --bucket ppmt-amp-data-sync-${ACCOUNT_ID} --key raw/ 2>/dev/null
aws s3api put-object --bucket ppmt-amp-data-sync-${ACCOUNT_ID} --key processed/ 2>/dev/null
aws s3api put-object --bucket ppmt-amp-data-sync-${ACCOUNT_ID} --key archived/ 2>/dev/null

echo ""
echo "Step 3: Creating IAM Role for Lambda..."
echo "---------------------------------------"

# Create role
aws iam create-role \
    --role-name ppmt-amp-lambda-role \
    --assume-role-policy-document '{
        "Version": "2012-10-17",
        "Statement": [{
            "Effect": "Allow",
            "Principal": {"Service": "lambda.amazonaws.com"},
            "Action": "sts:AssumeRole"
        }]
    }' 2>/dev/null && echo -e "${GREEN}✓ Lambda role created${NC}" || echo -e "${YELLOW}Role may already exist${NC}"

# Attach basic execution policy
aws iam attach-role-policy \
    --role-name ppmt-amp-lambda-role \
    --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole \
    2>/dev/null

# Create custom policy
cat > /tmp/lambda-policy.json << EOF
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": ["dynamodb:*"],
            "Resource": [
                "arn:aws:dynamodb:${REGION}:${ACCOUNT_ID}:table/PPMT-AMP-Prices",
                "arn:aws:dynamodb:${REGION}:${ACCOUNT_ID}:table/PPMT-AMP-Prices/index/*",
                "arn:aws:dynamodb:${REGION}:${ACCOUNT_ID}:table/PPMT-AMP-RateLimits"
            ]
        },
        {
            "Effect": "Allow",
            "Action": ["s3:*"],
            "Resource": [
                "arn:aws:s3:::ppmt-amp-data-sync-${ACCOUNT_ID}",
                "arn:aws:s3:::ppmt-amp-data-sync-${ACCOUNT_ID}/*",
                "arn:aws:s3:::ppmt-amp-exports-${ACCOUNT_ID}",
                "arn:aws:s3:::ppmt-amp-exports-${ACCOUNT_ID}/*"
            ]
        }
    ]
}
EOF

aws iam put-role-policy \
    --role-name ppmt-amp-lambda-role \
    --policy-name ppmt-amp-access-policy \
    --policy-document file:///tmp/lambda-policy.json 2>/dev/null

ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/ppmt-amp-lambda-role"
echo -e "${GREEN}✓ Policies attached${NC}"

echo "Waiting for IAM role to propagate..."
sleep 15

echo ""
echo "Step 4: Creating Lambda Function..."
echo "-----------------------------------"

# Package Lambda
cd lambda
zip -q lambda_function.zip price_query_handler.py 2>/dev/null || true

# Create function
aws lambda create-function \
    --function-name ppmt-amp-price-query \
    --runtime python3.11 \
    --role $ROLE_ARN \
    --handler price_query_handler.lambda_handler \
    --zip-file fileb://lambda_function.zip \
    --timeout 30 \
    --memory-size 256 \
    --environment Variables="{APP_SECRET=change-this-secret-key-in-production}" \
    --region $REGION \
    2>/dev/null && echo -e "${GREEN}✓ Lambda function created${NC}" || echo -e "${YELLOW}Function may already exist${NC}"

cd ..

LAMBDA_ARN="arn:aws:lambda:${REGION}:${ACCOUNT_ID}:function:ppmt-amp-price-query"

echo ""
echo "Step 5: Creating API Gateway..."
echo "-------------------------------"

# Create API
API_ID=$(aws apigateway create-rest-api \
    --name "PPMT-AMP-API" \
    --description "API for PPMT-AMP price queries" \
    --endpoint-configuration types=REGIONAL \
    --region $REGION \
    --query 'id' --output text 2>/dev/null || \
    aws apigateway get-rest-apis --query "items[?name=='PPMT-AMP-API'].id" --output text)

echo -e "${GREEN}✓ API Gateway created: $API_ID${NC}"

# Get root resource
ROOT_ID=$(aws apigateway get-resources --rest-api-id $API_ID --query "items[?path=='/'].id" --output text --region $REGION)

# Create /prices resource
PRICES_RESOURCE_ID=$(aws apigateway create-resource \
    --rest-api-id $API_ID \
    --parent-id $ROOT_ID \
    --path-part prices \
    --region $REGION \
    --query 'id' --output text 2>/dev/null || \
    aws apigateway get-resources --rest-api-id $API_ID --query "items[?path=='/prices'].id" --output text --region $REGION)

# Create GET method
aws apigateway put-method \
    --rest-api-id $API_ID \
    --resource-id $PRICES_RESOURCE_ID \
    --http-method GET \
    --authorization-type NONE \
    --region $REGION 2>/dev/null

# Integrate with Lambda
aws apigateway put-integration \
    --rest-api-id $API_ID \
    --resource-id $PRICES_RESOURCE_ID \
    --http-method GET \
    --type AWS_PROXY \
    --integration-http-method POST \
    --uri "arn:aws:apigateway:${REGION}:lambda:path/2015-03-31/functions/${LAMBDA_ARN}/invocations" \
    --region $REGION 2>/dev/null

# Grant permission
aws lambda add-permission \
    --function-name ppmt-amp-price-query \
    --statement-id apigateway-invoke-${API_ID} \
    --action lambda:InvokeFunction \
    --principal apigateway.amazonaws.com \
    --source-arn "arn:aws:execute-api:${REGION}:${ACCOUNT_ID}:${API_ID}/*" \
    --region $REGION 2>/dev/null

# Deploy API
aws apigateway create-deployment \
    --rest-api-id $API_ID \
    --stage-name prod \
    --region $REGION 2>/dev/null && echo -e "${GREEN}✓ API deployed${NC}"

API_ENDPOINT="https://${API_ID}.execute-api.${REGION}.amazonaws.com/prod"

echo ""
echo "=========================================="
echo "Setup Complete!"
echo "=========================================="
echo ""
echo -e "${GREEN}API Endpoint:${NC} $API_ENDPOINT"
echo -e "${GREEN}S3 Data Bucket:${NC} s3://ppmt-amp-data-sync-${ACCOUNT_ID}"
echo -e "${GREEN}S3 Export Bucket:${NC} s3://ppmt-amp-exports-${ACCOUNT_ID}"
echo ""
echo "Next Steps:"
echo "1. Update config/appsettings.json with API endpoint: $API_ENDPOINT"
echo "2. Update app secret key in both iOS app and Lambda environment"
echo "3. Test API: curl \"${API_ENDPOINT}/prices?appId=ppmt-amp-ios-v1&deviceId=test&timestamp=\$(date +%s)&signature=test&limit=10\""
echo "4. Rebuild iOS app: dotnet build"
echo ""
echo "View logs: aws logs tail /aws/lambda/ppmt-amp-price-query --follow"
echo ""

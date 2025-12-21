# AWS Lambda Deployment Guide for PPMT-AMP API

## Overview
This Lambda function provides a secure API for querying price data with:
- App signature verification (HMAC-SHA256)
- Rate limiting (20 requests per 5 minutes per device)
- Source verification (only from legitimate app)

## Prerequisites
1. AWS Account with appropriate permissions
2. AWS CLI configured
3. Python 3.11 runtime

## DynamoDB Tables Required

### 1. PPMT-AMP-Prices Table
```bash
aws dynamodb create-table \
    --table-name PPMT-AMP-Prices \
    --attribute-definitions \
        AttributeName=Id,AttributeType=S \
        AttributeName=PriceDate,AttributeType=S \
    --key-schema \
        AttributeName=Id,KeyType=HASH \
    --global-secondary-indexes \
        "[{\"IndexName\":\"DateIndex\",\"KeySchema\":[{\"AttributeName\":\"PriceDate\",\"KeyType\":\"HASH\"}],\"Projection\":{\"ProjectionType\":\"ALL\"},\"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}}]" \
    --provisioned-throughput \
        ReadCapacityUnits=5,WriteCapacityUnits=5
```

### 2. PPMT-AMP-RateLimits Table
```bash
aws dynamodb create-table \
    --table-name PPMT-AMP-RateLimits \
    --attribute-definitions \
        AttributeName=deviceId,AttributeType=S \
    --key-schema \
        AttributeName=deviceId,KeyType=HASH \
    --provisioned-throughput \
        ReadCapacityUnits=5,WriteCapacityUnits=5
```

## Lambda Function Deployment

### 1. Create IAM Role for Lambda
```bash
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
```

### 2. Attach Policies
```bash
# CloudWatch Logs
aws iam attach-role-policy \
    --role-name ppmt-amp-lambda-role \
    --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole

# DynamoDB Access
aws iam put-role-policy \
    --role-name ppmt-amp-lambda-role \
    --policy-name DynamoDBAccess \
    --policy-document '{
        "Version": "2012-10-17",
        "Statement": [{
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
                "arn:aws:dynamodb:*:*:table/PPMT-AMP-RateLimits"
            ]
        }]
    }'
```

### 3. Create Lambda Function
```bash
# Zip the function
zip lambda_function.zip price_query_handler.py

# Create function
aws lambda create-function \
    --function-name ppmt-amp-price-query \
    --runtime python3.11 \
    --role arn:aws:iam::YOUR_ACCOUNT_ID:role/ppmt-amp-lambda-role \
    --handler price_query_handler.lambda_handler \
    --zip-file fileb://lambda_function.zip \
    --timeout 30 \
    --memory-size 256 \
    --environment Variables="{APP_SECRET=your-app-secret-key}"
```

## API Gateway Setup

### 1. Create REST API
```bash
aws apigateway create-rest-api \
    --name "PPMT-AMP-API" \
    --description "API for PPMT-AMP price queries"
```

### 2. Create Resource and Method
```bash
# Get API ID
API_ID=$(aws apigateway get-rest-apis --query "items[?name=='PPMT-AMP-API'].id" --output text)

# Get root resource ID
ROOT_ID=$(aws apigateway get-resources --rest-api-id $API_ID --query "items[?path=='/'].id" --output text)

# Create /prices resource
aws apigateway create-resource \
    --rest-api-id $API_ID \
    --parent-id $ROOT_ID \
    --path-part prices

# Get prices resource ID
PRICES_ID=$(aws apigateway get-resources --rest-api-id $API_ID --query "items[?path=='/prices'].id" --output text)

# Create GET method
aws apigateway put-method \
    --rest-api-id $API_ID \
    --resource-id $PRICES_ID \
    --http-method GET \
    --authorization-type NONE
```

### 3. Integrate with Lambda
```bash
LAMBDA_ARN="arn:aws:lambda:REGION:ACCOUNT_ID:function:ppmt-amp-price-query"

aws apigateway put-integration \
    --rest-api-id $API_ID \
    --resource-id $PRICES_ID \
    --http-method GET \
    --type AWS_PROXY \
    --integration-http-method POST \
    --uri "arn:aws:apigateway:REGION:lambda:path/2015-03-31/functions/$LAMBDA_ARN/invocations"
```

### 4. Grant API Gateway Permission
```bash
aws lambda add-permission \
    --function-name ppmt-amp-price-query \
    --statement-id apigateway-invoke \
    --action lambda:InvokeFunction \
    --principal apigateway.amazonaws.com \
    --source-arn "arn:aws:execute-api:REGION:ACCOUNT_ID:$API_ID/*"
```

### 5. Deploy API
```bash
aws apigateway create-deployment \
    --rest-api-id $API_ID \
    --stage-name prod
```

### 6. Get API Endpoint
```bash
echo "https://$API_ID.execute-api.REGION.amazonaws.com/prod"
```

## Update iOS App Configuration

Update the API base URL in `src/PPMT-AMP.Core/Services/ApiClient.cs`:
```csharp
_apiBaseUrl = "https://YOUR_API_ID.execute-api.REGION.amazonaws.com/prod";
```

## Security Features

### 1. Signature Verification
- Every request must be signed with HMAC-SHA256
- Signature includes: appId, deviceId, timestamp, and payload
- Prevents unauthorized API access

### 2. Rate Limiting
- 20 requests per 5 minutes per device
- Tracked in DynamoDB
- Prevents API abuse

### 3. Timestamp Validation
- Requests must be within 5-minute window
- Prevents replay attacks

### 4. App ID Verification
- Only requests from registered app IDs are allowed
- Blocks requests from unknown sources

## Testing

```bash
# Test Lambda directly
aws lambda invoke \
    --function-name ppmt-amp-price-query \
    --payload '{"queryStringParameters":{"appId":"ppmt-amp-ios-v1","deviceId":"test-device","timestamp":"'$(date +%s)'","signature":"test"}}' \
    response.json

# View response
cat response.json
```

## Monitoring

```bash
# View Lambda logs
aws logs tail /aws/lambda/ppmt-amp-price-query --follow

# View API Gateway logs
aws logs tail API-Gateway-Execution-Logs_$API_ID/prod --follow
```

## Cost Estimate (for light usage)
- Lambda: ~$0.20/month (1M requests)
- DynamoDB: ~$1.25/month (25 GB storage)
- API Gateway: ~$3.50/month (1M requests)
- **Total: ~$5/month**

## Next Steps
1. Deploy Lambda function
2. Set up API Gateway
3. Update iOS app with API endpoint
4. Test with real devices
5. Monitor usage and adjust rate limits as needed

# AWS Lambda Function for PPMT-AMP API Gateway
# This Lambda function handles price queries with rate limiting and app signature verification

import json
import hmac
import hashlib
import base64
import time
import os
from datetime import datetime, timedelta
from decimal import Decimal

# DynamoDB table names
ITEMS_TABLE = "PPMT-AMP-Items"  # Individual blind box items with pricing
SERIES_TABLE = "PPMT-AMP-Series"  # Series-level information
RATE_LIMIT_TABLE = "PPMT-AMP-RateLimits"

# Rate limiting configuration
RATE_LIMIT_WINDOW = 300  # 5 minutes in seconds
RATE_LIMIT_MAX_REQUESTS = 20

# App verification
APP_SECRET = os.environ.get('APP_SECRET', 'your-secret-key-change-this-in-production')
VALID_APP_IDS = ["ppmt-amp-ios-v1"]

def verify_signature(app_id, device_id, timestamp, payload, signature):
    """Verify HMAC-SHA256 signature to ensure request is from legitimate app"""
    message = f"{app_id}:{device_id}:{timestamp}:{payload}"
    expected_signature = hmac.new(
        APP_SECRET.encode(),
        message.encode(),
        hashlib.sha256
    ).digest()
    expected_signature_b64 = base64.b64encode(expected_signature).decode()
    
    print(f"DEBUG: message={message}")
    print(f"DEBUG: expected={expected_signature_b64}")
    print(f"DEBUG: received={signature}")
    
    return hmac.compare_digest(signature, expected_signature_b64)

def check_rate_limit(dynamodb, device_id):
    """Check if device has exceeded rate limit"""
    try:
        response = dynamodb.get_item(
            TableName=RATE_LIMIT_TABLE,
            Key={'deviceId': {'S': device_id}}
        )
        
        if 'Item' not in response:
            # First request from this device
            return True, RATE_LIMIT_MAX_REQUESTS
        
        item = response['Item']
        request_count = int(item.get('requestCount', {}).get('N', 0))
        window_start = float(item.get('windowStart', {}).get('N', 0))
        
        current_time = time.time()
        
        # Check if we're in a new window
        if current_time - window_start > RATE_LIMIT_WINDOW:
            # New window, reset counter
            return True, RATE_LIMIT_MAX_REQUESTS
        
        # Check if limit exceeded
        if request_count >= RATE_LIMIT_MAX_REQUESTS:
            remaining_time = RATE_LIMIT_WINDOW - (current_time - window_start)
            return False, 0
        
        return True, RATE_LIMIT_MAX_REQUESTS - request_count
        
    except Exception as e:
        print(f"Rate limit check error: {e}")
        # Allow request on error to avoid blocking legitimate users
        return True, RATE_LIMIT_MAX_REQUESTS

def update_rate_limit(dynamodb, device_id):
    """Update rate limit counter for device"""
    try:
        current_time = time.time()
        
        # Get current count
        response = dynamodb.get_item(
            TableName=RATE_LIMIT_TABLE,
            Key={'deviceId': {'S': device_id}}
        )
        
        if 'Item' not in response:
            # First request
            dynamodb.put_item(
                TableName=RATE_LIMIT_TABLE,
                Item={
                    'deviceId': {'S': device_id},
                    'requestCount': {'N': '1'},
                    'windowStart': {'N': str(current_time)},
                    'lastRequest': {'N': str(current_time)}
                }
            )
        else:
            item = response['Item']
            request_count = int(item.get('requestCount', {}).get('N', 0))
            window_start = float(item.get('windowStart', {}).get('N', 0))
            
            # Check if new window
            if current_time - window_start > RATE_LIMIT_WINDOW:
                # Reset counter
                dynamodb.put_item(
                    TableName=RATE_LIMIT_TABLE,
                    Item={
                        'deviceId': {'S': device_id},
                        'requestCount': {'N': '1'},
                        'windowStart': {'N': str(current_time)},
                        'lastRequest': {'N': str(current_time)}
                    }
                )
            else:
                # Increment counter
                dynamodb.update_item(
                    TableName=RATE_LIMIT_TABLE,
                    Key={'deviceId': {'S': device_id}},
                    UpdateExpression='SET requestCount = requestCount + :inc, lastRequest = :time',
                    ExpressionAttributeValues={
                        ':inc': {'N': '1'},
                        ':time': {'N': str(current_time)}
                    }
                )
                
    except Exception as e:
        print(f"Rate limit update error: {e}")

def query_prices(dynamodb, series_id=None, product_id=None, ip_character=None, category=None, rarity=None, start_date=None, end_date=None, limit=50):
    """Query price data from DynamoDB with new PopMart schema"""
    try:
        # If SeriesId is provided, use Query (most efficient)
        if series_id:
            params = {
                'TableName': ITEMS_TABLE,
                'KeyConditionExpression': 'SeriesId = :sid',
                'ExpressionAttributeValues': {
                    ':sid': {'S': series_id}
                },
                'Limit': limit
            }
            
            # Add optional ProductId range key condition
            if product_id:
                params['KeyConditionExpression'] += ' AND ProductId = :pid'
                params['ExpressionAttributeValues'][':pid'] = {'S': product_id}
            
            response = dynamodb.query(**params)
            return response.get('Items', [])
        
        # If IpCharacter is provided, use GSI query
        if ip_character:
            params = {
                'TableName': ITEMS_TABLE,
                'IndexName': 'IpCharacter-Timestamp-Index',
                'KeyConditionExpression': 'IpCharacter = :ip',
                'ExpressionAttributeValues': {
                    ':ip': {'S': ip_character}
                },
                'ScanIndexForward': False,  # Latest timestamp first
                'Limit': limit
            }
            response = dynamodb.query(**params)
            return response.get('Items', [])
        
        # Otherwise use scan with filters
        params = {
            'TableName': ITEMS_TABLE,
            'Limit': limit
        }
        
        # Add filters if provided
        filter_expressions = []
        expression_values = {}
        
        if product_id:
            filter_expressions.append('ProductId = :pid')
            expression_values[':pid'] = {'S': product_id}
        
        if category:
            filter_expressions.append('Category = :cat')
            expression_values[':cat'] = {'S': category}
        
        if rarity:
            filter_expressions.append('Rarity = :rarity')
            expression_values[':rarity'] = {'S': rarity}
        
        if start_date:
            filter_expressions.append('#ts >= :start')
            expression_values[':start'] = {'S': start_date}
            if 'ExpressionAttributeNames' not in params:
                params['ExpressionAttributeNames'] = {}
            params['ExpressionAttributeNames']['#ts'] = 'Timestamp'
        
        if end_date:
            filter_expressions.append('#ts <= :end')
            expression_values[':end'] = {'S': end_date}
            if 'ExpressionAttributeNames' not in params:
                params['ExpressionAttributeNames'] = {}
            params['ExpressionAttributeNames']['#ts'] = 'Timestamp'
        
        if filter_expressions:
            params['FilterExpression'] = ' AND '.join(filter_expressions)
            params['ExpressionAttributeValues'] = expression_values
        
        response = dynamodb.scan(**params)
        
        return response.get('Items', [])
        
    except Exception as e:
        print(f"Query error: {e}")
        return []

def lambda_handler(event, context):
    """Main Lambda handler for API Gateway requests"""
    import boto3
    import base64
    
    # Handle warmup requests from EventBridge (keeps Lambda container warm)
    # This prevents cold starts by pinging Lambda every 5 minutes
    if event.get('source') == 'aws.events':
        print("EventBridge warmup ping - container staying warm")
        return {
            'statusCode': 200,
            'body': json.dumps({'status': 'warm', 'message': 'Container ready'})
        }
    
    # Handle warmup requests with custom payload
    if event.get('warmup') == True:
        print("Warmup request received - keeping container alive")
        return {
            'statusCode': 200,
            'body': json.dumps({'status': 'warm', 'message': 'Container ready'})
        }
    
    dynamodb = boto3.client('dynamodb')
    
    # Parse request
    query_params = event.get('queryStringParameters', {})
    
    # Extract verification parameters
    app_id = query_params.get('appId')
    device_id = query_params.get('deviceId')
    timestamp = query_params.get('timestamp')
    signature = query_params.get('signature')
    
    # Verification 1: Check if request is from valid app
    if not app_id or app_id not in VALID_APP_IDS:
        return {
            'statusCode': 403,
            'body': json.dumps({
                'success': False,
                'message': 'Invalid app identifier'
            })
        }
    
    # Verification 2: Check timestamp (prevent replay attacks)
    try:
        request_time = int(timestamp)
        current_time = int(time.time())
        if abs(current_time - request_time) > 300:  # 5 minute window
            return {
                'statusCode': 403,
                'body': json.dumps({
                    'success': False,
                    'message': 'Request timestamp expired'
                })
            }
    except:
        return {
            'statusCode': 400,
            'body': json.dumps({
                'success': False,
                'message': 'Invalid timestamp'
            })
        }
    
    # Verification 3: Check signature
    # Verify method + path
    http_method = event.get('httpMethod', 'GET')
    path = event.get('path', '/prices')
    payload = f"{http_method}:{path}"
    
    if not verify_signature(app_id, device_id, timestamp, payload, signature):
        return {
            'statusCode': 403,
            'body': json.dumps({
                'success': False,
                'message': 'Invalid request signature'
            })
        }
    
    # Verification 4: Check rate limit
    allowed, remaining = check_rate_limit(dynamodb, device_id)
    
    if not allowed:
        return {
            'statusCode': 429,
            'body': json.dumps({
                'success': False,
                'message': 'Rate limit exceeded. Please try again later.',
                'rateLimitRemaining': 0,
                'rateLimitReset': datetime.utcnow() + timedelta(minutes=5)
            })
        }
    
    # Parse query parameters for price query
    series_id = query_params.get('seriesId')
    product_id = query_params.get('productId')
    ip_character = query_params.get('ipCharacter')
    category = query_params.get('category')
    rarity = query_params.get('rarity')
    start_date = query_params.get('startDate')
    end_date = query_params.get('endDate')
    limit = int(query_params.get('limit', '50'))
    
    # Update rate limit
    update_rate_limit(dynamodb, device_id)
    
    # Query prices
    results = query_prices(
        dynamodb,
        series_id=series_id,
        product_id=product_id,
        ip_character=ip_character,
        category=category,
        rarity=rarity,
        start_date=start_date,
        end_date=end_date,
        limit=int(limit)
    )
    
    # Return response
    return {
        'statusCode': 200,
        'headers': {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*'
        },
        'body': json.dumps({
            'success': True,
            'message': 'Query successful',
            'data': results,
            'rateLimitRemaining': remaining - 1,
            'rateLimitReset': datetime.utcnow() + timedelta(seconds=RATE_LIMIT_WINDOW)
        }, default=str)
    }

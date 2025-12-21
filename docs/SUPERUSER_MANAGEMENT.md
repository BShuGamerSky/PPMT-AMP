# Superuser Management Portal (Phase 3)

## Overview

Phase 3 adds a **superuser management interface** directly in the iOS app, allowing authorized administrators to create, update, and delete price records in real-time. This provides a simple data ingestion path for manual corrections and additions without requiring backend infrastructure changes.

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│                   iOS App (Superuser)                     │
│  ┌────────────────────────────────────────────────────┐  │
│  │  MainViewController                                 │  │
│  │  ├── Query Prices (read-only)                      │  │
│  │  ├── ➕ Add New Price                              │  │
│  │  ├── ✏️ Edit Existing Price                        │  │
│  │  ├── 🗑️ Delete Price                              │  │
│  │  └── 📋 View Audit Logs                            │  │
│  └────────────────────────────────────────────────────┘  │
└───────────────────────┬──────────────────────────────────┘
                        │ HTTPS + HMAC Signatures
                        ▼
┌──────────────────────────────────────────────────────────┐
│                   API Gateway                             │
│  ├── POST /prices/create                                 │
│  ├── POST /prices/update                                 │
│  ├── DELETE /prices/{id}                                 │
│  └── GET /audit-logs                                     │
└───────────────────────┬──────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────┐
│            Lambda (price_management_handler)              │
│  1. Verify HMAC signature                                │
│  2. Check rate limits                                    │
│  3. Validate user role via Cognito                       │
│  4. Validate price data                                  │
│  5. Perform DynamoDB operation                           │
│  6. Log to audit trail                                   │
└───────────────────────┬──────────────────────────────────┘
                        │
            ┌───────────┴─────────────┐
            ▼                         ▼
┌─────────────────────────┐  ┌─────────────────────────┐
│  DynamoDB               │  │  DynamoDB               │
│  PPMT-AMP-Prices        │  │  PPMT-AMP-AuditLog      │
│  - Create/Update/Delete │  │  - Who/What/When        │
└─────────────────────────┘  └─────────────────────────┘
```

---

## User Roles

### 1. **Visitor** (Current)
- ✅ Query prices (read-only)
- ✅ Rate limited (20 req/5min)
- ❌ No authentication required

### 2. **User** (Future)
- ✅ Query prices (read-only)
- ✅ Save preferences
- ✅ Custom rate limits
- ✅ Authenticated via Cognito

### 3. **Superuser** (Phase 3 - Priority)
- ✅ All user permissions
- ✅ **Create** new price records
- ✅ **Update** existing prices
- ✅ **Delete** records
- ✅ View audit logs
- ✅ **Exempt from rate limits**
- ✅ Authenticated via Cognito
- ✅ Role verified on every request

---

## Implementation Details

### 1. Cognito User Pool

**Purpose:** Authenticate users and manage role-based access

**Setup:**
```bash
# Create User Pool
aws cognito-idp create-user-pool \
  --pool-name PPMT-AMP-Users \
  --policies "PasswordPolicy={MinimumLength=8,RequireUppercase=true,RequireLowercase=true,RequireNumbers=true}" \
  --auto-verified-attributes email \
  --region us-east-1

# Create User Pool Client
aws cognito-idp create-user-pool-client \
  --user-pool-id <pool-id> \
  --client-name PPMT-AMP-iOS \
  --no-generate-secret \
  --explicit-auth-flows ALLOW_USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH
```

**User Attributes:**
```json
{
  "Username": "admin@ppmt-amp.com",
  "Attributes": [
    {"Name": "email", "Value": "admin@ppmt-amp.com"},
    {"Name": "custom:role", "Value": "superuser"},
    {"Name": "custom:deviceId", "Value": "admin-device-001"}
  ]
}
```

**Create Superuser:**
```bash
aws cognito-idp admin-create-user \
  --user-pool-id <pool-id> \
  --username admin@ppmt-amp.com \
  --user-attributes Name=email,Value=admin@ppmt-amp.com Name=custom:role,Value=superuser \
  --temporary-password TempPass123! \
  --message-action SUPPRESS
```

---

### 2. DynamoDB Tables

#### PPMT-AMP-Users
```python
{
    "TableName": "PPMT-AMP-Users",
    "KeySchema": [
        {"AttributeName": "userId", "KeyType": "HASH"}
    ],
    "AttributeDefinitions": [
        {"AttributeName": "userId", "AttributeType": "S"},
        {"AttributeName": "role", "AttributeType": "S"}
    ],
    "GlobalSecondaryIndexes": [
        {
            "IndexName": "RoleIndex",
            "KeySchema": [
                {"AttributeName": "role", "KeyType": "HASH"}
            ],
            "Projection": {"ProjectionType": "ALL"}
        }
    ],
    "BillingMode": "PAY_PER_REQUEST"
}
```

**Sample Record:**
```json
{
  "userId": "cognito-user-id-123",
  "email": "admin@ppmt-amp.com",
  "username": "admin",
  "role": "superuser",
  "deviceId": "admin-device-001",
  "createdAt": "2025-12-21T10:00:00Z",
  "lastLogin": "2025-12-21T15:30:00Z",
  "preferences": {
    "notifications": true,
    "theme": "dark"
  }
}
```

#### PPMT-AMP-AuditLog
```python
{
    "TableName": "PPMT-AMP-AuditLog",
    "KeySchema": [
        {"AttributeName": "logId", "KeyType": "HASH"},
        {"AttributeName": "timestamp", "KeyType": "RANGE"}
    ],
    "AttributeDefinitions": [
        {"AttributeName": "logId", "AttributeType": "S"},
        {"AttributeName": "timestamp", "AttributeType": "N"},
        {"AttributeName": "userId", "AttributeType": "S"}
    ],
    "GlobalSecondaryIndexes": [
        {
            "IndexName": "UserIndex",
            "KeySchema": [
                {"AttributeName": "userId", "KeyType": "HASH"},
                {"AttributeName": "timestamp", "KeyType": "RANGE"}
            ],
            "Projection": {"ProjectionType": "ALL"}
        }
    ],
    "TimeToLiveSpecification": {
        "Enabled": true,
        "AttributeName": "ttl"
    },
    "BillingMode": "PAY_PER_REQUEST"
}
```

**Sample Record:**
```json
{
  "logId": "log-20251221-001",
  "timestamp": 1703161800,
  "userId": "cognito-user-id-123",
  "action": "update",
  "priceId": "price-001",
  "oldValue": {
    "MarketPrice": "1299.00",
    "RetailPrice": "1499.00"
  },
  "newValue": {
    "MarketPrice": "1199.00",
    "RetailPrice": "1399.00"
  },
  "ipAddress": "203.0.113.45",
  "userAgent": "PPMT-AMP-iOS/1.0",
  "ttl": 1710937800  // 90 days from now
}
```

---

### 3. Lambda Function (price_management_handler)

**Function:** `ppmt-amp-price-management`
**Runtime:** Python 3.11
**Timeout:** 30 seconds
**Memory:** 512 MB

**Environment Variables:**
```
APP_SECRET=your-secret-key-change-this-in-production
COGNITO_USER_POOL_ID=us-east-1_XXXXXX
PRICES_TABLE=PPMT-AMP-Prices
AUDIT_LOG_TABLE=PPMT-AMP-AuditLog
USERS_TABLE=PPMT-AMP-Users
```

**Implementation:**
```python
import json
import boto3
import hmac
import hashlib
import base64
import time
import uuid
import os
from decimal import Decimal

dynamodb = boto3.resource('dynamodb')
cognito = boto3.client('cognito-idp')

prices_table = dynamodb.Table(os.environ['PRICES_TABLE'])
audit_table = dynamodb.Table(os.environ['AUDIT_LOG_TABLE'])
users_table = dynamodb.Table(os.environ['USERS_TABLE'])

def lambda_handler(event, context):
    """
    Handle price management operations (create/update/delete)
    """
    
    # Parse request
    http_method = event['httpMethod']
    path = event['path']
    body = json.loads(event.get('body', '{}'))
    headers = event.get('headers', {})
    
    # 1. Verify signature
    if not verify_signature(event):
        return {
            'statusCode': 403,
            'body': json.dumps({'error': 'Invalid signature'})
        }
    
    # 2. Extract user info
    id_token = headers.get('Authorization', '').replace('Bearer ', '')
    user_info = verify_cognito_token(id_token)
    
    if not user_info:
        return {
            'statusCode': 401,
            'body': json.dumps({'error': 'Unauthorized'})
        }
    
    # 3. Check role
    if user_info.get('custom:role') != 'superuser':
        return {
            'statusCode': 403,
            'body': json.dumps({'error': 'Insufficient permissions. Superuser required.'})
        }
    
    # 4. Route to appropriate handler
    if path == '/prices/create' and http_method == 'POST':
        return handle_create(body, user_info)
    elif path == '/prices/update' and http_method == 'POST':
        return handle_update(body, user_info)
    elif path.startswith('/prices/') and http_method == 'DELETE':
        price_id = path.split('/')[-1]
        return handle_delete(price_id, user_info)
    elif path == '/audit-logs' and http_method == 'GET':
        return handle_audit_logs(event.get('queryStringParameters', {}))
    else:
        return {
            'statusCode': 404,
            'body': json.dumps({'error': 'Not found'})
        }

def verify_signature(event):
    """Verify HMAC-SHA256 signature"""
    headers = event.get('headers', {})
    signature = headers.get('X-Signature', '')
    timestamp = headers.get('X-Timestamp', '')
    app_id = headers.get('X-App-Id', '')
    device_id = headers.get('X-Device-Id', '')
    
    # Check timestamp (within 5 minutes)
    if abs(int(time.time()) - int(timestamp)) > 300:
        print("Timestamp expired")
        return False
    
    # Construct message
    http_method = event['httpMethod']
    path = event['path']
    message = f"{app_id}:{device_id}:{timestamp}:{http_method}:{path}"
    
    # Calculate expected signature
    secret = os.environ['APP_SECRET']
    expected_signature = base64.b64encode(
        hmac.new(secret.encode(), message.encode(), hashlib.sha256).digest()
    ).decode()
    
    print(f"Expected: {expected_signature}, Received: {signature}")
    return hmac.compare_digest(expected_signature, signature)

def verify_cognito_token(id_token):
    """Verify Cognito ID token and return user attributes"""
    try:
        response = cognito.get_user(AccessToken=id_token)
        
        # Convert attributes list to dict
        attributes = {}
        for attr in response['UserAttributes']:
            attributes[attr['Name']] = attr['Value']
        
        attributes['username'] = response['Username']
        return attributes
    except Exception as e:
        print(f"Cognito verification failed: {str(e)}")
        return None

def handle_create(body, user_info):
    """Create new price record"""
    try:
        # Validate required fields
        required_fields = ['ProductId', 'ProductName', 'MarketPrice', 'RetailPrice', 'Currency']
        for field in required_fields:
            if field not in body:
                return {
                    'statusCode': 400,
                    'body': json.dumps({'error': f'Missing required field: {field}'})
                }
        
        # Generate new ID
        price_id = f"price-{uuid.uuid4().hex[:8]}"
        timestamp = time.time()
        
        # Create item
        item = {
            'Id': price_id,
            'ProductId': body['ProductId'],
            'ProductName': body['ProductName'],
            'MarketPrice': str(body['MarketPrice']),
            'RetailPrice': str(body['RetailPrice']),
            'Currency': body['Currency'],
            'PriceDate': body.get('PriceDate', time.strftime('%Y-%m-%d')),
            'Category': body.get('Category', 'Other'),
            'Source': 'superuser-manual',
            'Status': 'active',
            'CreatedAt': timestamp,
            'UpdatedAt': timestamp,
            'CreatedBy': user_info['username']
        }
        
        # Write to DynamoDB
        prices_table.put_item(Item=item)
        
        # Log to audit trail
        log_audit(
            user_info=user_info,
            action='create',
            price_id=price_id,
            old_value=None,
            new_value=item
        )
        
        return {
            'statusCode': 201,
            'body': json.dumps({
                'message': 'Price created successfully',
                'priceId': price_id
            })
        }
    except Exception as e:
        print(f"Create error: {str(e)}")
        return {
            'statusCode': 500,
            'body': json.dumps({'error': str(e)})
        }

def handle_update(body, user_info):
    """Update existing price record"""
    try:
        price_id = body.get('Id')
        if not price_id:
            return {
                'statusCode': 400,
                'body': json.dumps({'error': 'Missing price Id'})
            }
        
        # Get existing record
        response = prices_table.get_item(Key={'Id': price_id})
        if 'Item' not in response:
            return {
                'statusCode': 404,
                'body': json.dumps({'error': 'Price not found'})
            }
        
        old_item = response['Item']
        
        # Build update expression
        update_expr = "SET UpdatedAt = :updated, UpdatedBy = :user"
        expr_values = {
            ':updated': time.time(),
            ':user': user_info['username']
        }
        
        # Update only provided fields
        if 'MarketPrice' in body:
            update_expr += ", MarketPrice = :mp"
            expr_values[':mp'] = str(body['MarketPrice'])
        
        if 'RetailPrice' in body:
            update_expr += ", RetailPrice = :rp"
            expr_values[':rp'] = str(body['RetailPrice'])
        
        if 'ProductName' in body:
            update_expr += ", ProductName = :pn"
            expr_values[':pn'] = body['ProductName']
        
        if 'Category' in body:
            update_expr += ", Category = :cat"
            expr_values[':cat'] = body['Category']
        
        if 'Status' in body:
            update_expr += ", #status = :status"
            expr_values[':status'] = body['Status']
        
        # Perform update
        prices_table.update_item(
            Key={'Id': price_id},
            UpdateExpression=update_expr,
            ExpressionAttributeValues=expr_values,
            ExpressionAttributeNames={'#status': 'Status'} if 'Status' in body else None
        )
        
        # Get updated item
        response = prices_table.get_item(Key={'Id': price_id})
        new_item = response['Item']
        
        # Log to audit trail
        log_audit(
            user_info=user_info,
            action='update',
            price_id=price_id,
            old_value=old_item,
            new_value=new_item
        )
        
        return {
            'statusCode': 200,
            'body': json.dumps({
                'message': 'Price updated successfully',
                'updatedFields': list(body.keys())
            })
        }
    except Exception as e:
        print(f"Update error: {str(e)}")
        return {
            'statusCode': 500,
            'body': json.dumps({'error': str(e)})
        }

def handle_delete(price_id, user_info):
    """Delete price record"""
    try:
        # Get existing record for audit log
        response = prices_table.get_item(Key={'Id': price_id})
        if 'Item' not in response:
            return {
                'statusCode': 404,
                'body': json.dumps({'error': 'Price not found'})
            }
        
        old_item = response['Item']
        
        # Delete from DynamoDB
        prices_table.delete_item(Key={'Id': price_id})
        
        # Log to audit trail
        log_audit(
            user_info=user_info,
            action='delete',
            price_id=price_id,
            old_value=old_item,
            new_value=None
        )
        
        return {
            'statusCode': 200,
            'body': json.dumps({'message': 'Price deleted successfully'})
        }
    except Exception as e:
        print(f"Delete error: {str(e)}")
        return {
            'statusCode': 500,
            'body': json.dumps({'error': str(e)})
        }

def handle_audit_logs(query_params):
    """Retrieve audit logs"""
    try:
        user_id = query_params.get('userId')
        limit = int(query_params.get('limit', 50))
        
        if user_id:
            # Query by user
            response = audit_table.query(
                IndexName='UserIndex',
                KeyConditionExpression='userId = :uid',
                ExpressionAttributeValues={':uid': user_id},
                Limit=limit,
                ScanIndexForward=False  # Newest first
            )
        else:
            # Scan all logs
            response = audit_table.scan(Limit=limit)
        
        return {
            'statusCode': 200,
            'body': json.dumps({
                'logs': response['Items'],
                'count': len(response['Items'])
            }, default=str)
        }
    except Exception as e:
        print(f"Audit logs error: {str(e)}")
        return {
            'statusCode': 500,
            'body': json.dumps({'error': str(e)})
        }

def log_audit(user_info, action, price_id, old_value, new_value):
    """Write to audit log table"""
    try:
        timestamp = int(time.time())
        log_id = f"log-{uuid.uuid4().hex[:12]}"
        
        item = {
            'logId': log_id,
            'timestamp': timestamp,
            'userId': user_info.get('sub', user_info['username']),
            'username': user_info['username'],
            'action': action,
            'priceId': price_id,
            'ttl': timestamp + (90 * 24 * 60 * 60)  # 90 days TTL
        }
        
        if old_value:
            item['oldValue'] = old_value
        
        if new_value:
            item['newValue'] = new_value
        
        audit_table.put_item(Item=item)
        print(f"Audit log created: {log_id}")
    except Exception as e:
        print(f"Audit log error: {str(e)}")
```

---

### 4. iOS App Implementation

#### Update AuthService (Add Cognito)
```csharp
// src/PPMT-AMP.Core/Services/AuthService.cs
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;

public class AuthService
{
    private static AuthService? _instance;
    private string _role = "visitor";
    private string? _idToken;
    private string? _accessToken;
    private string? _username;
    
    private readonly AmazonCognitoIdentityProviderClient _cognitoClient;
    private readonly string _userPoolId;
    private readonly string _clientId;
    
    private AuthService()
    {
        var config = AppConfiguration.Instance;
        _userPoolId = config.GetValue("AWS:Cognito:UserPoolId") ?? "";
        _clientId = config.GetValue("AWS:Cognito:ClientId") ?? "";
        
        _cognitoClient = new AmazonCognitoIdentityProviderClient(
            Amazon.RegionEndpoint.GetBySystemName(
                config.GetAWSRegion()
            )
        );
    }
    
    public static AuthService Instance => _instance ??= new AuthService();
    
    public async Task<bool> LoginWithCognitoAsync(string username, string password)
    {
        try
        {
            var authRequest = new InitiateAuthRequest
            {
                ClientId = _clientId,
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                AuthParameters = new Dictionary<string, string>
                {
                    { "USERNAME", username },
                    { "PASSWORD", password }
                }
            };
            
            var response = await _cognitoClient.InitiateAuthAsync(authRequest);
            
            _idToken = response.AuthenticationResult.IdToken;
            _accessToken = response.AuthenticationResult.AccessToken;
            _username = username;
            
            // Get user attributes to determine role
            var userResponse = await _cognitoClient.GetUserAsync(new GetUserRequest
            {
                AccessToken = _accessToken
            });
            
            var roleAttr = userResponse.UserAttributes
                .FirstOrDefault(a => a.Name == "custom:role");
            
            _role = roleAttr?.Value ?? "user";
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cognito login failed: {ex.Message}");
            return false;
        }
    }
    
    public bool IsSuperuser => _role == "superuser";
    public string? IdToken => _idToken;
    public string? Username => _username;
}
```

#### Update ApiClient (Add Superuser Methods)
```csharp
// src/PPMT-AMP.Core/Services/ApiClient.cs

public async Task<string> CreatePriceAsync(PriceData price)
{
    if (!AuthService.Instance.IsSuperuser)
        throw new UnauthorizedAccessException("Superuser role required");
    
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    var payload = "POST:/prices/create";
    var signature = GenerateSignature(payload, timestamp);
    
    var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}/prices/create");
    request.Headers.Add("X-Signature", signature);
    request.Headers.Add("X-Timestamp", timestamp);
    request.Headers.Add("X-App-Id", _appId);
    request.Headers.Add("X-Device-Id", _deviceId);
    request.Headers.Add("Authorization", $"Bearer {AuthService.Instance.IdToken}");
    
    var jsonBody = JsonConvert.SerializeObject(price);
    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
    
    var response = await _httpClient.SendAsync(request);
    return await response.Content.ReadAsStringAsync();
}

public async Task<string> UpdatePriceAsync(PriceData price)
{
    if (!AuthService.Instance.IsSuperuser)
        throw new UnauthorizedAccessException("Superuser role required");
    
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    var payload = "POST:/prices/update";
    var signature = GenerateSignature(payload, timestamp);
    
    var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}/prices/update");
    request.Headers.Add("X-Signature", signature);
    request.Headers.Add("X-Timestamp", timestamp);
    request.Headers.Add("X-App-Id", _appId);
    request.Headers.Add("X-Device-Id", _deviceId);
    request.Headers.Add("Authorization", $"Bearer {AuthService.Instance.IdToken}");
    
    var jsonBody = JsonConvert.SerializeObject(price);
    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
    
    var response = await _httpClient.SendAsync(request);
    return await response.Content.ReadAsStringAsync();
}

public async Task<string> DeletePriceAsync(string priceId)
{
    if (!AuthService.Instance.IsSuperuser)
        throw new UnauthorizedAccessException("Superuser role required");
    
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    var payload = $"DELETE:/prices/{priceId}";
    var signature = GenerateSignature(payload, timestamp);
    
    var request = new HttpRequestMessage(HttpMethod.Delete, $"{_apiBaseUrl}/prices/{priceId}");
    request.Headers.Add("X-Signature", signature);
    request.Headers.Add("X-Timestamp", timestamp);
    request.Headers.Add("X-App-Id", _appId);
    request.Headers.Add("X-Device-Id", _deviceId);
    request.Headers.Add("Authorization", $"Bearer {AuthService.Instance.IdToken}");
    
    var response = await _httpClient.SendAsync(request);
    return await response.Content.ReadAsStringAsync();
}
```

#### Update MainViewController (Add Superuser UI)
```csharp
// src/PPMT-AMP.iOS/MainViewController.cs

public override void ViewDidLoad()
{
    base.ViewDidLoad();
    
    // ... existing code ...
    
    // Show superuser controls if applicable
    if (AuthService.Instance.IsSuperuser)
    {
        SetupSuperuserUI();
    }
}

private void SetupSuperuserUI()
{
    var addButton = new UIButton(UIButtonType.System)
    {
        Frame = new CGRect(20, 250, View.Bounds.Width - 40, 44),
        BackgroundColor = UIColor.FromRGB(52, 199, 89),
        TintColor = UIColor.White
    };
    addButton.SetTitle("➕ Add New Price", UIControlState.Normal);
    addButton.Layer.CornerRadius = 8;
    addButton.TouchUpInside += OnAddPriceClicked;
    View.AddSubview(addButton);
    
    var auditButton = new UIButton(UIButtonType.System)
    {
        Frame = new CGRect(20, 304, View.Bounds.Width - 40, 44),
        BackgroundColor = UIColor.FromRGB(255, 149, 0)
    };
    auditButton.SetTitle("📋 View Audit Logs", UIControlState.Normal);
    auditButton.Layer.CornerRadius = 8;
    auditButton.TouchUpInside += OnAuditLogsClicked;
    View.AddSubview(auditButton);
    
    // Enable swipe actions on table view
    _tableView.AllowsSwipeToDeleteButton = true;
}

private async void OnAddPriceClicked(object? sender, EventArgs e)
{
    var alert = new UIAlertController
    {
        Title = "Add New Price",
        Message = "Enter product details",
        PreferredStyle = UIAlertControllerStyle.Alert
    };
    
    alert.AddTextField(field => field.Placeholder = "Product ID");
    alert.AddTextField(field => field.Placeholder = "Product Name");
    alert.AddTextField(field => { field.Placeholder = "Market Price"; field.KeyboardType = UIKeyboardType.DecimalPad; });
    alert.AddTextField(field => { field.Placeholder = "Retail Price"; field.KeyboardType = UIKeyboardType.DecimalPad; });
    
    alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
    alert.AddAction(UIAlertAction.Create("Create", UIAlertActionStyle.Default, async action =>
    {
        var price = new PriceData
        {
            ProductId = alert.TextFields[0].Text,
            ProductName = alert.TextFields[1].Text,
            MarketPrice = alert.TextFields[2].Text,
            RetailPrice = alert.TextFields[3].Text,
            Currency = "USD",
            Category = "Other"
        };
        
        var result = await _apiClient.CreatePriceAsync(price);
        ShowAlert("Success", $"Price created: {result}");
    }));
    
    PresentViewController(alert, true, null);
}

private async void OnAuditLogsClicked(object? sender, EventArgs e)
{
    // Navigate to audit log view (new view controller)
    var auditVC = new AuditLogViewController();
    NavigationController?.PushViewController(auditVC, true);
}
```

---

## Deployment Checklist

### Phase 3 Implementation Steps

- [ ] **1. Create Cognito User Pool**
  - [ ] Configure password policy
  - [ ] Add custom attribute: `custom:role`
  - [ ] Create iOS app client

- [ ] **2. Create DynamoDB Tables**
  - [ ] PPMT-AMP-Users (with RoleIndex GSI)
  - [ ] PPMT-AMP-AuditLog (with UserIndex GSI, TTL enabled)

- [ ] **3. Create Superuser Account**
  - [ ] Add admin user to Cognito
  - [ ] Set `custom:role = superuser`
  - [ ] Set temporary password

- [ ] **4. Deploy Lambda Function**
  - [ ] Package `price_management_handler.py`
  - [ ] Set environment variables
  - [ ] Attach IAM role (Cognito + DynamoDB permissions)

- [ ] **5. Update API Gateway**
  - [ ] Add POST /prices/create
  - [ ] Add POST /prices/update
  - [ ] Add DELETE /prices/{id}
  - [ ] Add GET /audit-logs

- [ ] **6. Update iOS App**
  - [ ] Add Cognito NuGet package
  - [ ] Update AuthService with Cognito login
  - [ ] Add superuser UI to MainViewController
  - [ ] Implement create/update/delete methods

- [ ] **7. Testing**
  - [ ] Test superuser login
  - [ ] Test create price
  - [ ] Test update price
  - [ ] Test delete price
  - [ ] Verify audit logs
  - [ ] Test role enforcement (try with non-superuser)

---

## Cost Estimation (Phase 3)

```
Cognito User Pool:
  Free tier: 50,000 MAUs
  Estimated: $0/month (well within free tier)

DynamoDB (2 new tables):
  PPMT-AMP-Users: ~$0.25/month (few users)
  PPMT-AMP-AuditLog: ~$1/month (with TTL cleanup)

Lambda (price-management):
  ~100 invocations/month
  ~$0.10/month

API Gateway:
  Additional 100 requests/month
  ~$0.01/month

Total Added Cost: ~$1.36/month
```

---

## Security Considerations

1. **HMAC Signatures:** All requests still require valid signatures
2. **Cognito Tokens:** ID tokens verified on every superuser operation
3. **Role Verification:** Lambda checks `custom:role = superuser` attribute
4. **Audit Trail:** All changes logged with who/what/when
5. **Rate Limit Exemption:** Only for superusers, still signature-protected
6. **TTL on Audit Logs:** Automatic cleanup after 90 days

---

## Next Steps

After Phase 3 is complete, you'll have:
- ✅ Full CRUD operations for prices
- ✅ Simple iOS-based admin portal
- ✅ Complete audit trail
- ✅ Role-based access control

This gives you a powerful, lightweight data ingestion path without needing separate admin tools!

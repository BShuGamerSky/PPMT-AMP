# API Signature Payload Strategy Analysis

## Current Implementation: Empty Payload

### Signature Format
```
message = "{appId}:{deviceId}:{timestamp}:"
         = "ppmt-amp-ios-v1:UUID:1766284450:"
```

### Advantages ✅
1. **Simplicity**: Easy to implement, no query param parsing in signature
2. **GET Request Compliance**: Matches REST best practices (GET = no body)
3. **Smaller Signature**: Shorter message = faster HMAC computation
4. **Query Flexibility**: Can change query params without breaking signature
5. **Easier Debugging**: Fewer variables to track in logs

### Disadvantages ❌
1. **Less Security**: Query params (limit, productId) not verified in signature
2. **Parameter Tampering**: Attacker could modify `limit=10` to `limit=99999` without breaking signature
3. **No Request Integrity**: Can't verify exact request was authorized

---

## Alternative: Full Payload Signing

### Signature Format
```
message = "{appId}:{deviceId}:{timestamp}:query:{productId}:{category}:{startDate}:{endDate}:{limit}"
         = "ppmt-amp-ios-v1:UUID:1766284450:query:PROD-001:Electronics:2025-12-01:2025-12-20:50"
```

### Advantages ✅
1. **Full Request Integrity**: Every query parameter is signed
2. **Parameter Tampering Protection**: Changing `limit` invalidates signature
3. **Audit Trail**: Exact request parameters are cryptographically verified
4. **Defense in Depth**: Even if attacker gets valid signature, can't modify request

### Disadvantages ❌
1. **Complexity**: Must rebuild exact payload on both client and server
2. **Order Sensitivity**: Params must be in identical order (sorting required)
3. **Optional Params**: Need consistent handling of `None`/`null` values
4. **URL Length**: Longer query strings for signature computation
5. **Debugging Harder**: More points of failure if payload format differs

---

## Hybrid Approach: Critical Params Only

### Signature Format (Recommended for Production)
```
message = "{appId}:{deviceId}:{timestamp}:{method}:{path}"
         = "ppmt-amp-ios-v1:UUID:1766284450:GET:/prices"
```

### Advantages ✅
1. **Method Protection**: Prevents method tampering (GET → DELETE)
2. **Path Protection**: Prevents endpoint switching (/prices → /admin)
3. **Balance**: Core security without complexity overhead
4. **Flexible Queries**: Non-critical params (limit, filters) can vary

### Implementation
```csharp
// iOS App
var message = $"{_appId}:{_deviceId}:{timestamp}:GET:/prices";
var signature = GenerateSignature(message);

// Lambda
message = f"{app_id}:{device_id}:{timestamp}:{http_method}:{path}"
expected = generate_signature(message)
```

---

## Recommendation by Environment

### **Development (Current)**
- **Payload**: Empty `""`
- **Rationale**: Simplicity for rapid iteration
- **Security**: Rate limiting provides baseline protection

### **Staging**
- **Payload**: Method + Path `"GET:/prices"`
- **Rationale**: Test production-grade security
- **Security**: Prevents endpoint/method tampering

### **Production**
- **Payload**: Method + Path + Critical Params `"GET:/prices:limit"`
- **Rationale**: Maximum security for public API
- **Security**: Full request verification

---

## Migration Path

### Phase 1: Current (Empty Payload) ✅
```python
# Lambda
payload = ""
message = f"{app_id}:{device_id}:{timestamp}:"
```

### Phase 2: Add Method/Path
```python
# Lambda
http_method = event['httpMethod']
path = event['path']
payload = f"{http_method}:{path}"
message = f"{app_id}:{device_id}:{timestamp}:{payload}"
```

### Phase 3: Add Critical Params (Optional)
```python
# Lambda
limit = query_params.get('limit', '50')
payload = f"{http_method}:{path}:{limit}"
message = f"{app_id}:{device_id}:{timestamp}:{payload}"
```

---

## Security Considerations

### What Empty Payload DOES Protect ✅
- Prevents replay attacks (timestamp verification)
- Prevents unauthorized apps (appId validation)
- Prevents device spoofing (deviceId tracking)
- Prevents signature forgery (HMAC-SHA256)
- Rate limiting per device (DynamoDB tracking)

### What Empty Payload DOES NOT Protect ❌
- Query parameter tampering
- Request method switching
- Endpoint switching
- Large query abuse (e.g., limit=999999)

### Additional Protections Needed
1. **Server-Side Validation**: Enforce max limits regardless of client request
2. **Input Sanitization**: Validate all query params in Lambda
3. **Business Logic**: Reject unreasonable requests (e.g., limit > 1000)

---

## Code Example: Enhanced Security

```python
# Lambda with server-side enforcement
def lambda_handler(event, context):
    # Extract params
    limit = int(query_params.get('limit', '50'))
    
    # Enforce server-side limits
    if limit > 1000:
        return {
            'statusCode': 400,
            'body': json.dumps({'error': 'limit exceeds maximum of 1000'})
        }
    
    # Verify signature (with empty payload for now)
    payload = ""
    if not verify_signature(app_id, device_id, timestamp, payload, signature):
        return {'statusCode': 403, 'body': json.dumps({'error': 'Invalid signature'})}
    
    # Process request with enforced limit
    results = query_dynamodb(limit=min(limit, 1000))
```

---

## Conclusion

**Current empty payload is acceptable for MVP** because:
1. Server enforces business rules
2. Rate limiting prevents abuse
3. Signature prevents unauthorized access
4. Development velocity is maintained

**For production, recommend Phase 2** (Method + Path):
- Adds critical security
- Minimal complexity increase
- Easy to implement
- Industry standard practice

**Avoid Phase 3 unless**:
- Handling financial transactions
- Regulatory compliance required (PCI-DSS, HIPAA)
- High-value data queries

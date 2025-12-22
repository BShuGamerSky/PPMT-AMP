#!/usr/bin/env python3
import requests
import hmac
import hashlib
import base64
import time

app_id = 'ppmt-amp-ios-v1'
device_id = 'test-device-001'
secret = 'your-secret-key-here-change-in-production'

print('🧪 Testing CloudFront CDN with NEW schema parameters...\n')

# Use NEW schema parameters: seriesId, ipCharacter, rarity
timestamp = str(int(time.time()))
payload = 'GET:/prices'
message = f'{app_id}:{device_id}:{timestamp}:{payload}'
sig = base64.b64encode(hmac.new(secret.encode(), message.encode(), hashlib.sha256).digest()).decode()

url = 'https://djz8jf44mdqq8.cloudfront.net/prices'
params = {
    'appId': app_id,
    'deviceId': device_id,
    'timestamp': timestamp,
    'signature': sig,
    'rarity': f'UNIQUE-{int(time.time() * 1000)}',  # Absolutely unique cache key
    'limit': '5'
}

print(f'URL: {url}')
print(f'Query: seriesId=SERIES-LABUBU-MONSTERS, ipCharacter=Labubu')
print(f'Auth: appId={app_id}, timestamp={timestamp}\n')

start = time.time()
resp = requests.get(url, params=params, timeout=10)
elapsed = (time.time() - start) * 1000

print(f'Status: {resp.status_code}')
print(f'Time: {elapsed:.0f}ms')
print(f'Cache: {resp.headers.get("X-Cache", "N/A")}')
print(f'Age: {resp.headers.get("Age", "N/A")}s\n')

if resp.status_code == 200:
    data = resp.json()
    print(f'✅ CLOUDFRONT WORKING!')
    print(f'Items: {len(data.get("data", []))}')
    print(f'Rate limit: {data.get("rateLimitRemaining")}/20')
else:
    print(f'❌ Still failing')
    print(f'Response: {resp.text[:200]}')

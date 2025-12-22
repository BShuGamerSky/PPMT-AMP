#!/usr/bin/env python3
"""
Test script to measure API Gateway + Lambda query speed
"""
import hmac
import hashlib
import base64
import time
import requests
from datetime import datetime

# Configuration (from appsettings.json)
API_BASE_URL = "https://stou0wlmf4.execute-api.us-east-1.amazonaws.com/prod"
APP_ID = "ppmt-amp-ios-v1"
APP_SECRET = "your-secret-key-change-this-in-production"
DEVICE_ID = "test-device-12345"

def generate_signature(payload, timestamp):
    """Generate HMAC-SHA256 signature matching iOS app logic"""
    message = f"{APP_ID}:{DEVICE_ID}:{timestamp}:{payload}"
    signature = hmac.new(
        APP_SECRET.encode(),
        message.encode(),
        hashlib.sha256
    ).digest()
    return base64.b64encode(signature).decode()

def test_query_speed(num_requests=5):
    """Test API query speed with multiple requests"""
    print(f"Testing API query speed with {num_requests} requests...\n")
    
    times = []
    
    for i in range(num_requests):
        timestamp = int(time.time())
        payload = "GET:/prices"
        signature = generate_signature(payload, timestamp)
        
        # Build request
        params = {
            "appId": APP_ID,
            "deviceId": DEVICE_ID,
            "timestamp": str(timestamp),
            "signature": signature,
            "limit": "50"
        }
        
        url = f"{API_BASE_URL}/prices"
        
        # Measure request time
        start = time.time()
        try:
            response = requests.get(url, params=params, timeout=30)
            elapsed_ms = (time.time() - start) * 1000
            
            times.append(elapsed_ms)
            
            print(f"Request {i+1}:")
            print(f"  Status: {response.status_code}")
            print(f"  Time: {elapsed_ms:.2f}ms")
            
            if response.status_code == 200:
                data = response.json()
                if data.get("success"):
                    record_count = len(data.get("data", []))
                    print(f"  Records: {record_count}")
                else:
                    print(f"  Message: {data.get('message')}")
            else:
                print(f"  Body: {response.text[:200]}")
            
        except Exception as e:
            print(f"Request {i+1}: ERROR - {e}")
        
        print()
        
        # Wait between requests to avoid rate limiting
        if i < num_requests - 1:
            time.sleep(1)
    
    # Statistics
    if times:
        avg_time = sum(times) / len(times)
        min_time = min(times)
        max_time = max(times)
        
        print("=" * 50)
        print("Statistics:")
        print(f"  Average: {avg_time:.2f}ms")
        print(f"  Minimum: {min_time:.2f}ms")
        print(f"  Maximum: {max_time:.2f}ms")
        print(f"  Total requests: {len(times)}")

if __name__ == "__main__":
    test_query_speed()

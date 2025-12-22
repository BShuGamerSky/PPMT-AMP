#!/usr/bin/env python3
"""
Test script to verify API Gateway query endpoint performance
Tests the signature verification and measures response time
"""

import hmac
import hashlib
import base64
import time
import requests
import json

# Configuration
API_BASE_URL = "https://stou0wlmf4.execute-api.us-east-1.amazonaws.com/prod"
APP_ID = "ppmt-amp-ios-v1"
APP_SECRET = "your-secret-key-change-this-in-production"
DEVICE_ID = "test-device-123"

def generate_signature(app_id, device_id, timestamp, payload, secret):
    """Generate HMAC-SHA256 signature matching C# implementation"""
    message = f"{app_id}:{device_id}:{timestamp}:{payload}"
    signature = hmac.new(
        secret.encode(),
        message.encode(),
        hashlib.sha256
    ).digest()
    signature_b64 = base64.b64encode(signature).decode()
    
    print(f"Message: {message}")
    print(f"Signature: {signature_b64}")
    
    return signature_b64

def test_query_prices():
    """Test price query endpoint with signature verification"""
    
    # Create request parameters
    timestamp = int(time.time())
    payload = "GET:/prices"
    signature = generate_signature(APP_ID, DEVICE_ID, timestamp, payload, APP_SECRET)
    
    # Build query parameters
    params = {
        "appId": APP_ID,
        "deviceId": DEVICE_ID,
        "timestamp": str(timestamp),
        "signature": signature,
        "limit": "50"
    }
    
    url = f"{API_BASE_URL}/prices"
    
    print(f"\n{'='*60}")
    print(f"Testing API Query Performance")
    print(f"{'='*60}")
    print(f"URL: {url}")
    print(f"Params: {json.dumps(params, indent=2)}")
    print(f"{'='*60}\n")
    
    # Measure response time
    start_time = time.time()
    
    try:
        response = requests.get(url, params=params, timeout=30)
        elapsed_ms = (time.time() - start_time) * 1000
        
        print(f"Status Code: {response.status_code}")
        print(f"Response Time: {elapsed_ms:.2f}ms")
        print(f"Response Headers: {dict(response.headers)}")
        print(f"\nResponse Body:")
        print(json.dumps(response.json(), indent=2))
        
        if response.status_code == 200:
            print(f"\n✅ SUCCESS - Query completed in {elapsed_ms:.2f}ms")
        else:
            print(f"\n❌ FAILED - Status {response.status_code}")
            
    except Exception as e:
        elapsed_ms = (time.time() - start_time) * 1000
        print(f"❌ ERROR after {elapsed_ms:.2f}ms: {str(e)}")

def run_performance_tests(num_tests=5):
    """Run multiple tests and calculate statistics"""
    print(f"\n{'='*60}")
    print(f"Running {num_tests} Performance Tests")
    print(f"{'='*60}\n")
    
    response_times = []
    successful = 0
    
    for i in range(num_tests):
        timestamp = int(time.time())
        payload = "GET:/prices"
        signature = generate_signature(APP_ID, DEVICE_ID, timestamp, payload, APP_SECRET)
        
        params = {
            "appId": APP_ID,
            "deviceId": DEVICE_ID,
            "timestamp": str(timestamp),
            "signature": signature,
            "limit": "50"
        }
        
        url = f"{API_BASE_URL}/prices"
        
        start_time = time.time()
        try:
            response = requests.get(url, params=params, timeout=30)
            elapsed_ms = (time.time() - start_time) * 1000
            response_times.append(elapsed_ms)
            
            status_icon = "✅" if response.status_code == 200 else "❌"
            print(f"Test {i+1}/{num_tests}: {status_icon} {elapsed_ms:.2f}ms (Status: {response.status_code})")
            
            if response.status_code == 200:
                successful += 1
                
            # Small delay between requests to avoid rate limiting
            time.sleep(0.5)
            
        except Exception as e:
            elapsed_ms = (time.time() - start_time) * 1000
            print(f"Test {i+1}/{num_tests}: ❌ {elapsed_ms:.2f}ms (Error: {str(e)})")
    
    # Calculate statistics
    if response_times:
        avg_time = sum(response_times) / len(response_times)
        min_time = min(response_times)
        max_time = max(response_times)
        
        print(f"\n{'='*60}")
        print(f"Performance Summary")
        print(f"{'='*60}")
        print(f"Total Tests:     {num_tests}")
        print(f"Successful:      {successful} ({successful/num_tests*100:.1f}%)")
        print(f"Failed:          {num_tests - successful}")
        print(f"Average Time:    {avg_time:.2f}ms")
        print(f"Min Time:        {min_time:.2f}ms")
        print(f"Max Time:        {max_time:.2f}ms")
        print(f"{'='*60}\n")

if __name__ == "__main__":
    import sys
    
    # Run single detailed test first
    test_query_prices()
    
    # Then run performance tests
    print("\n")
    num_tests = int(sys.argv[1]) if len(sys.argv) > 1 else 10
    run_performance_tests(num_tests)

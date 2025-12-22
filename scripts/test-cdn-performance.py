#!/usr/bin/env python3
"""
Test CloudFront CDN performance vs direct API Gateway
Compares latency and demonstrates caching benefits
"""

import time
import requests
import hmac
import hashlib
import base64
import statistics

# Configuration
API_GATEWAY_URL = "https://stou0wlmf4.execute-api.us-east-1.amazonaws.com/prod"
CLOUDFRONT_URL = "https://dkhagvq34al16.cloudfront.net"
APP_ID = "ppmt-amp-ios-v1"
APP_SECRET = "your-secret-key-change-this-in-production"
DEVICE_ID = "cdn-test-device"

def generate_signature(app_id, device_id, timestamp, payload, secret):
    """Generate HMAC-SHA256 signature"""
    message = f"{app_id}:{device_id}:{timestamp}:{payload}"
    signature = hmac.new(secret.encode(), message.encode(), hashlib.sha256).digest()
    return base64.b64encode(signature).decode()

def test_endpoint(url, endpoint_name, num_tests=10):
    """Test an endpoint multiple times"""
    print(f"\n{'='*70}")
    print(f"Testing: {endpoint_name}")
    print(f"URL: {url}")
    print(f"{'='*70}\n")
    
    times = []
    cache_hits = 0
    
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
        
        full_url = f"{url}/prices"
        
        start = time.time()
        try:
            response = requests.get(full_url, params=params, timeout=30)
            elapsed_ms = (time.time() - start) * 1000
            times.append(elapsed_ms)
            
            # Check if response came from cache
            cache_status = response.headers.get('X-Cache', 'N/A')
            age = response.headers.get('Age', 'N/A')
            
            if 'Hit from cloudfront' in cache_status:
                cache_hits += 1
                cache_icon = "🔥"
            else:
                cache_icon = "  "
            
            print(f"Test {i+1:2d}: {cache_icon} {elapsed_ms:6.2f}ms - Status {response.status_code} - Cache: {cache_status} - Age: {age}s")
            
            time.sleep(0.5)
            
        except Exception as e:
            print(f"Test {i+1:2d}: ERROR - {e}")
    
    if times:
        avg = statistics.mean(times)
        median = statistics.median(times)
        min_time = min(times)
        max_time = max(times)
        
        print(f"\n{'-'*70}")
        print(f"Summary for {endpoint_name}:")
        print(f"  Average:     {avg:.2f}ms")
        print(f"  Median:      {median:.2f}ms")
        print(f"  Min:         {min_time:.2f}ms")
        print(f"  Max:         {max_time:.2f}ms")
        print(f"  Cache Hits:  {cache_hits}/{num_tests} ({cache_hits/num_tests*100:.0f}%)")
        print(f"{'-'*70}")
        
        return {
            'avg': avg,
            'median': median,
            'min': min_time,
            'max': max_time,
            'cache_hits': cache_hits,
            'total': num_tests
        }
    
    return None

def main():
    print("="*70)
    print("CloudFront CDN Performance Comparison")
    print("="*70)
    print("\nWaiting for CloudFront to finish deploying...")
    print("Checking status...")
    
    # Check CloudFront status
    import subprocess
    result = subprocess.run(
        ["aws", "cloudfront", "get-distribution", "--id", "E24OK55UTVZ2E3", 
         "--query", "Distribution.Status", "--output", "text"],
        capture_output=True,
        text=True
    )
    
    status = result.stdout.strip()
    print(f"CloudFront Status: {status}")
    
    if status == "InProgress":
        print("\n⚠️  CloudFront is still deploying (typically takes 15-20 minutes)")
        print("You can proceed with tests, but cache may not work optimally yet.\n")
        input("Press Enter to continue anyway, or Ctrl+C to exit...")
    
    # Test direct API Gateway
    print("\n" + "="*70)
    print("PHASE 1: Testing Direct API Gateway (No CDN)")
    print("="*70)
    api_results = test_endpoint(API_GATEWAY_URL, "Direct API Gateway", num_tests=10)
    
    # Test CloudFront CDN
    print("\n" + "="*70)
    print("PHASE 2: Testing CloudFront CDN (With Caching)")
    print("="*70)
    print("Note: First request will be MISS (cache population)")
    print("      Subsequent requests should be HIT (from cache)\n")
    cdn_results = test_endpoint(CLOUDFRONT_URL, "CloudFront CDN", num_tests=10)
    
    # Comparison
    if api_results and cdn_results:
        print("\n" + "="*70)
        print("COMPARISON RESULTS")
        print("="*70)
        
        improvement_avg = api_results['avg'] - cdn_results['avg']
        improvement_pct = (improvement_avg / api_results['avg']) * 100
        
        print(f"\nDirect API Gateway:")
        print(f"  Average: {api_results['avg']:.2f}ms")
        print(f"  Median:  {api_results['median']:.2f}ms")
        print(f"  Range:   {api_results['min']:.2f}-{api_results['max']:.2f}ms")
        
        print(f"\nCloudFront CDN:")
        print(f"  Average: {cdn_results['avg']:.2f}ms")
        print(f"  Median:  {cdn_results['median']:.2f}ms")
        print(f"  Range:   {cdn_results['min']:.2f}-{cdn_results['max']:.2f}ms")
        print(f"  Cache Hit Rate: {cdn_results['cache_hits']}/{cdn_results['total']} ({cdn_results['cache_hits']/cdn_results['total']*100:.0f}%)")
        
        print(f"\nImprovement:")
        if improvement_avg > 0:
            print(f"  ✅ {improvement_avg:.2f}ms faster ({improvement_pct:.1f}% improvement)")
        else:
            print(f"  ⚠️  {abs(improvement_avg):.2f}ms slower (CDN overhead)")
        
        print(f"\nCache Performance:")
        if cdn_results['cache_hits'] > 0:
            cache_times = [t for t in []]  # Would need to track separately
            print(f"  ✅ {cdn_results['cache_hits']} cached responses")
            print(f"  💰 Saved Lambda invocations: {cdn_results['cache_hits']}")
        else:
            print(f"  ⚠️  No cache hits yet (CDN still warming up)")
        
        print("\n" + "="*70)
        print("RECOMMENDATION")
        print("="*70)
        
        if cdn_results['cache_hits'] > 5:
            print("\n✅ CloudFront is working well!")
            print(f"   Cache hit rate: {cdn_results['cache_hits']/cdn_results['total']*100:.0f}%")
            print(f"   Average improvement: {improvement_pct:.1f}%")
            print("\n   Update iOS app to use CloudFront URL:")
            print(f"   {CLOUDFRONT_URL}")
        else:
            print("\n⏳ CloudFront needs more time to deploy and warm up")
            print("   Wait 5-10 minutes and test again")
            print(f"   Re-run: python3 scripts/test-cdn-performance.py")

if __name__ == "__main__":
    main()

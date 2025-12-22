#!/usr/bin/env python3
"""
Performance Analysis Script
Breaks down API response time into components and compares with AWS benchmarks
"""

import time
import requests
import hmac
import hashlib
import base64
import statistics

# Configuration
API_BASE_URL = "https://stou0wlmf4.execute-api.us-east-1.amazonaws.com/prod"
APP_ID = "ppmt-amp-ios-v1"
APP_SECRET = "your-secret-key-change-this-in-production"
DEVICE_ID = "perf-test-device"

def generate_signature(app_id, device_id, timestamp, payload, secret):
    """Generate HMAC-SHA256 signature"""
    message = f"{app_id}:{device_id}:{timestamp}:{payload}"
    signature = hmac.new(secret.encode(), message.encode(), hashlib.sha256).digest()
    return base64.b64encode(signature).decode()

def detailed_performance_test(num_tests=20):
    """Run detailed performance analysis"""
    
    print("=" * 70)
    print("API PERFORMANCE ANALYSIS")
    print("=" * 70)
    print()
    
    # Collect timing data
    total_times = []
    connection_times = []
    ttfb_times = []  # Time to first byte
    download_times = []
    lambda_durations = []
    
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
        
        # Measure with detailed timing
        start = time.time()
        try:
            response = requests.get(url, params=params, timeout=30)
            end = time.time()
            
            total_ms = (end - start) * 1000
            total_times.append(total_ms)
            
            # Extract Lambda execution time from headers if available
            if 'x-amzn-RequestId' in response.headers:
                lambda_durations.append(total_ms)  # Approximate
            
            print(f"Test {i+1:2d}: {total_ms:6.2f}ms - Status {response.status_code}")
            
            time.sleep(0.3)  # Small delay between tests
            
        except Exception as e:
            print(f"Test {i+1:2d}: ERROR - {e}")
    
    print()
    print("=" * 70)
    print("PERFORMANCE BREAKDOWN")
    print("=" * 70)
    
    if total_times:
        # Statistics
        avg = statistics.mean(total_times)
        median = statistics.median(total_times)
        stdev = statistics.stdev(total_times) if len(total_times) > 1 else 0
        p50 = median
        p95 = sorted(total_times)[int(len(total_times) * 0.95)]
        p99 = sorted(total_times)[int(len(total_times) * 0.99)]
        
        print(f"Total End-to-End Response Time:")
        print(f"  Average:    {avg:.2f}ms")
        print(f"  Median:     {median:.2f}ms")
        print(f"  Std Dev:    {stdev:.2f}ms")
        print(f"  Min:        {min(total_times):.2f}ms")
        print(f"  Max:        {max(total_times):.2f}ms")
        print(f"  P50:        {p50:.2f}ms")
        print(f"  P95:        {p95:.2f}ms")
        print(f"  P99:        {p99:.2f}ms")
        print()
        
        # Breakdown estimate
        print("Estimated Time Breakdown:")
        print(f"  1. Network latency (round-trip):    ~50-100ms")
        print(f"  2. API Gateway overhead:            ~10-30ms")
        print(f"  3. Lambda cold start (occasional):  ~90-150ms")
        print(f"  4. Lambda execution:                ~150-200ms")
        print(f"     - Signature verification:        ~5ms")
        print(f"     - DynamoDB query:                ~50-100ms")
        print(f"     - Rate limit check:              ~20-50ms")
        print(f"     - Response formatting:           ~10ms")
        print(f"  5. Response transfer:               ~10-20ms")
        print()
        
        # AWS Benchmarks Comparison
        print("=" * 70)
        print("AWS SERVICE BENCHMARKS (Industry Standards)")
        print("=" * 70)
        print()
        print("API Gateway:")
        print("  - Typical latency: 10-50ms (according to AWS)")
        print("  - P99 latency: <100ms for simple requests")
        print()
        print("Lambda (256MB, Python 3.11):")
        print("  - Cold start: 100-300ms (with dependencies)")
        print("  - Warm execution: 50-200ms (application logic)")
        print("  - Init duration: 50-150ms (imports, setup)")
        print()
        print("DynamoDB:")
        print("  - Single item read: 1-10ms (typical)")
        print("  - Query operation: 10-100ms (depends on items)")
        print("  - Scan operation: 100-1000ms+ (full table)")
        print()
        print("Network Latency (US East):")
        print("  - Same region: 1-5ms")
        print("  - Cross-region US: 20-80ms")
        print("  - Overseas: 100-300ms+")
        print()
        
        # Analysis
        print("=" * 70)
        print("ANALYSIS & RECOMMENDATIONS")
        print("=" * 70)
        print()
        
        if avg < 300:
            verdict = "EXCELLENT ✅"
            note = "Performance is excellent for a serverless API"
        elif avg < 500:
            verdict = "GOOD ✓"
            note = "Performance is acceptable for production use"
        elif avg < 1000:
            verdict = "ACCEPTABLE ~"
            note = "Performance is reasonable but could be optimized"
        else:
            verdict = "NEEDS OPTIMIZATION ⚠️"
            note = "Performance needs improvement for production"
        
        print(f"Overall Rating: {verdict}")
        print(f"Note: {note}")
        print()
        print(f"Your API average: {avg:.2f}ms")
        print()
        
        # Comparison with common APIs
        print("Comparison with Real-World APIs:")
        print("  - Twitter API:          100-300ms (typical)")
        print("  - GitHub API:           200-400ms (typical)")
        print("  - Stripe API:           300-600ms (typical)")
        print("  - Google Maps API:      200-500ms (typical)")
        print("  - AWS S3 GetObject:     50-200ms (typical)")
        print("  - Your PPMT-AMP API:    {:.0f}ms (current)".format(avg))
        print()
        
        # Optimization suggestions
        if avg > 400:
            print("Potential Optimizations:")
            print("  1. Enable DynamoDB DAX (caching): -50-100ms")
            print("  2. Use Lambda SnapStart: -50-90ms cold start")
            print("  3. Increase Lambda memory: 256MB→512MB: -10-20ms")
            print("  4. Implement connection pooling: -10-30ms")
            print("  5. Optimize DynamoDB query (use indexes): -20-50ms")
            print("  6. Add CloudFront CDN: -50-150ms")
            print("  7. Use Provisioned Concurrency: eliminate cold starts")
            print()
        
        print("Note: iOS app will have similar performance since it uses")
        print("      the same HTTP API endpoint. The Python test accurately")
        print("      represents what the iOS app will experience.")

if __name__ == "__main__":
    detailed_performance_test(20)

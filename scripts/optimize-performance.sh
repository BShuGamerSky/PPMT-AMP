#!/bin/bash
# Quick implementation guide for 484ms → 350-380ms optimization
# Total cost: ~$7/year, Performance gain: ~100-130ms (20-27% faster)

echo "======================================================================"
echo "PPMT-AMP Performance Optimization Implementation"
echo "======================================================================"
echo ""
echo "Current performance: 484ms average"
echo "Target performance: 350-380ms average (-20-27%)"
echo "Total cost: ~\$7/year"
echo ""

# Step 1: Increase Lambda Memory
echo "Step 1: Increase Lambda Memory (256MB → 512MB)"
echo "----------------------------------------------------------------------"
echo "Cost: +\$6.58/year | Benefit: -30-50ms execution time"
echo ""
echo "Command:"
echo "  aws lambda update-function-configuration \\"
echo "    --function-name ppmt-amp-price-query \\"
echo "    --memory-size 512 \\"
echo "    --region us-east-1"
echo ""
read -p "Execute Step 1? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    aws lambda update-function-configuration \
      --function-name ppmt-amp-price-query \
      --memory-size 512 \
      --region us-east-1
    echo "✓ Lambda memory increased to 512MB"
else
    echo "⊗ Skipped"
fi
echo ""

# Step 2: Deploy updated Lambda code
echo "Step 2: Deploy Updated Lambda Code (with warmup handling)"
echo "----------------------------------------------------------------------"
echo "Cost: \$0 | Benefit: Enables warmup functionality"
echo ""
echo "Command:"
echo "  cd lambda && zip function.zip price_query_handler.py"
echo "  aws lambda update-function-code \\"
echo "    --function-name ppmt-amp-price-query \\"
echo "    --zip-file fileb://function.zip \\"
echo "    --region us-east-1"
echo ""
read -p "Execute Step 2? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    cd lambda
    zip -q function.zip price_query_handler.py
    aws lambda update-function-code \
      --function-name ppmt-amp-price-query \
      --zip-file fileb://function.zip \
      --region us-east-1
    rm function.zip
    cd ..
    echo "✓ Lambda code updated with warmup handler"
else
    echo "⊗ Skipped"
fi
echo ""

# Step 3: Setup warmup schedule
echo "Step 3: Setup EventBridge Warmup (every 5 minutes)"
echo "----------------------------------------------------------------------"
echo "Cost: +\$0.14/year | Benefit: Eliminates cold starts (-100ms)"
echo ""
read -p "Execute Step 3? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    bash scripts/setup-lambda-warmup.sh
    echo "✓ Warmup schedule configured"
else
    echo "⊗ Skipped"
fi
echo ""

# Step 4: Test performance
echo "Step 4: Test New Performance"
echo "----------------------------------------------------------------------"
echo ""
echo "Wait 2-3 minutes for Lambda to warm up, then run:"
echo "  python3 scripts/analyze-performance.py"
echo ""
echo "Expected results:"
echo "  - Average: 350-380ms (from 484ms)"
echo "  - Min: 320-350ms"
echo "  - Max: 400-450ms"
echo "  - Consistency: ±20ms"
echo ""

echo "======================================================================"
echo "Implementation Complete!"
echo "======================================================================"
echo ""
echo "Summary:"
echo "  ✓ Lambda memory: 256MB → 512MB (+\$6.58/year)"
echo "  ✓ Warmup schedule: Every 5 minutes (+\$0.14/year)"
echo "  ✓ Total cost: +\$6.72/year"
echo "  ✓ Expected gain: -100-130ms (-20-27%)"
echo ""
echo "Next steps:"
echo "  1. Wait 2-3 minutes for container to warm up"
echo "  2. Run: python3 scripts/analyze-performance.py"
echo "  3. Compare before/after results"
echo ""

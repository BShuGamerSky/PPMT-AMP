#!/bin/bash
# Setup Lambda warmup with EventBridge to keep container hot
# Cost: ~$0.14/year, Performance gain: -100ms on cold starts

set -e

REGION="us-east-1"
ACCOUNT_ID="363416481362"
LAMBDA_NAME="ppmt-amp-price-query"
RULE_NAME="ppmt-amp-lambda-warmup"

echo "=========================================="
echo "Lambda Warmup Setup"
echo "=========================================="
echo ""

# Step 1: Create EventBridge rule
echo "1. Creating EventBridge rule (rate: 5 minutes)..."
aws events put-rule \
  --name "$RULE_NAME" \
  --schedule-expression "rate(5 minutes)" \
  --description "Keep Lambda warm - prevents cold starts" \
  --region "$REGION"

echo "   ✓ Rule created: $RULE_NAME"
echo ""

# Step 2: Add Lambda as target
echo "2. Adding Lambda function as target..."
LAMBDA_ARN="arn:aws:lambda:${REGION}:${ACCOUNT_ID}:function:${LAMBDA_NAME}"

aws events put-targets \
  --rule "$RULE_NAME" \
  --targets "Id"="1","Arn"="$LAMBDA_ARN","Input"='{"warmup":true}' \
  --region "$REGION"

echo "   ✓ Target configured: $LAMBDA_NAME"
echo ""

# Step 3: Grant EventBridge permission
echo "3. Granting EventBridge permission to invoke Lambda..."
aws lambda add-permission \
  --function-name "$LAMBDA_NAME" \
  --statement-id "EventBridgeWarmup" \
  --action 'lambda:InvokeFunction' \
  --principal events.amazonaws.com \
  --source-arn "arn:aws:events:${REGION}:${ACCOUNT_ID}:rule/${RULE_NAME}" \
  --region "$REGION" 2>/dev/null || echo "   (Permission already exists)"

echo "   ✓ Permission granted"
echo ""

# Step 4: Verify setup
echo "4. Verifying setup..."
RULE_STATUS=$(aws events describe-rule --name "$RULE_NAME" --region "$REGION" --query 'State' --output text)
echo "   Rule status: $RULE_STATUS"

TARGETS=$(aws events list-targets-by-rule --rule "$RULE_NAME" --region "$REGION" --query 'length(Targets)' --output text)
echo "   Targets configured: $TARGETS"
echo ""

echo "=========================================="
echo "Setup Complete! ✅"
echo "=========================================="
echo ""
echo "Lambda will be pinged every 5 minutes to stay warm."
echo ""
echo "Cost estimate:"
echo "  - EventBridge: ~\$0.12/year"
echo "  - Lambda invocations: ~\$0.02/year"
echo "  - Total: ~\$0.14/year"
echo ""
echo "Performance benefit:"
echo "  - Eliminates cold starts (~100ms penalty)"
echo "  - Lambda stays warm 24/7"
echo ""
echo "To monitor warmup invocations:"
echo "  aws logs tail /aws/lambda/$LAMBDA_NAME --follow | grep warmup"
echo ""
echo "To disable warmup:"
echo "  aws events disable-rule --name $RULE_NAME --region $REGION"
echo ""
echo "To remove warmup:"
echo "  aws events remove-targets --rule $RULE_NAME --ids 1 --region $REGION"
echo "  aws events delete-rule --name $RULE_NAME --region $REGION"
echo ""

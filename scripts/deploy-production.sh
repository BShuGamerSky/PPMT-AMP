#!/bin/bash
# Environment Deployment Script
# Supports: development, staging, production

set -e

# Parse environment argument
ENVIRONMENT=${1:-development}

echo "=========================================="
echo "PPMT-AMP Deployment: $ENVIRONMENT"
echo "=========================================="
echo ""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

case $ENVIRONMENT in
  development)
    echo -e "${GREEN}Development Environment${NC}"
    echo "Using known development secret (safe for local dev)"
    APP_SECRET="your-secret-key-change-this-in-production"
    LAMBDA_FUNCTION="ppmt-amp-price-query"
    ;;
    
  staging)
    echo -e "${YELLOW}Staging Environment${NC}"
    APP_SECRET=$(aws secretsmanager get-secret-value --secret-id ppmt-amp/staging/app-secret --query SecretString --output text 2>/dev/null || echo "")
    if [ -z "$APP_SECRET" ]; then
      echo "Generating new staging secret..."
      APP_SECRET=$(openssl rand -base64 32)
      aws secretsmanager create-secret --name ppmt-amp/staging/app-secret --secret-string "$APP_SECRET" 2>/dev/null
      echo -e "${GREEN}✓ Staging secret created${NC}"
    fi
    LAMBDA_FUNCTION="ppmt-amp-price-query-staging"
    ;;
    
  production)
    echo -e "${RED}Production Environment${NC}"
    read -p "Type 'YES' to continue: " confirm
    if [ "$confirm" != "YES" ]; then exit 1; fi
    APP_SECRET=$(aws secretsmanager get-secret-value --secret-id ppmt-amp/production/app-secret --query SecretString --output text 2>/dev/null || echo "")
    if [ -z "$APP_SECRET" ]; then
      APP_SECRET=$(openssl rand -base64 32)
      aws secretsmanager create-secret --name ppmt-amp/production/app-secret --secret-string "$APP_SECRET" 2>/dev/null
      echo -e "${GREEN}✓ Production secret created${NC}"
    fi
    LAMBDA_FUNCTION="ppmt-amp-price-query-prod"
    ;;
    
  *)
    echo -e "${RED}Error: Invalid environment${NC}"
    exit 1
    ;;
esac

echo -e "${GREEN}✓ Secret ready (${#APP_SECRET} characters)${NC}"
echo ""

# Update Lambda environment variable
echo "Updating Lambda function..."
aws lambda update-function-configuration \
    --function-name ppmt-amp-price-query \
    --environment "Variables={APP_SECRET=$APP_SECRET}" \
    --region us-east-1 \
    --query '[FunctionName,LastModified]' \
    --output text

echo ""
echo -e "${GREEN}✓ Lambda environment variable updated${NC}"

# Check if production config exists
if [ ! -f config/appsettings.production.json ]; then
    echo ""
    echo -e "${YELLOW}Warning: config/appsettings.production.json not found${NC}"
    echo "Creating from template..."
    
    cp config/appsettings.production.json.example config/appsettings.production.json
    
    # Replace APP_SECRET in config
    if [[ "$OSTYPE" == "darwin"* ]]; then
        sed -i '' "s|REPLACE_WITH_SECURE_SECRET_FROM_ENV|$APP_SECRET|g" config/appsettings.production.json
    else
        sed -i "s|REPLACE_WITH_SECURE_SECRET_FROM_ENV|$APP_SECRET|g" config/appsettings.production.json
    fi
    
    echo -e "${GREEN}✓ Production config created${NC}"
fi

echo ""
echo "=========================================="
echo "Production Setup Complete!"
echo "=========================================="
echo ""
echo "Next steps:"
echo "1. Update iOS build to use appsettings.production.json"
echo "2. Test in staging environment"
echo "3. Deploy to App Store"
echo ""
echo -e "${YELLOW}IMPORTANT: Never commit .env.production or appsettings.production.json to git!${NC}"
echo ""

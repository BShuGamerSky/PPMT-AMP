# PPMT-AMP: After-Market Price Management

A Xamarin.iOS mobile application for managing and analyzing after-market pricing data with AWS backend integration.

## Overview

PPMT-AMP is an iOS mobile app that provides real-time access to after-market pricing data. The app leverages AWS services for data storage, processing, and synchronization, enabling users to track price changes, upload data, and analyze market trends on the go.

## Features

### Current (v0.1 - MVP)
- ✅ **Visitor Mode**: Query prices without authentication
- ✅ **HMAC Security**: Signature-based request validation
- ✅ **Rate Limiting**: 20 requests per 5 minutes
- ✅ **Real-time Queries**: DynamoDB-backed price data via API Gateway
- ✅ **AWS Lambda Integration**: Serverless backend processing

### Planned (v0.2+)
- 🔄 **Automated ETL Pipeline**: Daily data scraping → S3 → Redshift → DynamoDB
- 🔄 **Superuser Management**: iOS-based admin portal for CRUD operations
- 🔄 **User Authentication**: Cognito-based login with role management
- 🔄 **Audit Logging**: Track all price changes with who/what/when
- 🔄 **CloudWatch Monitoring**: Dashboards and alerts

## Architecture

```
PPMT-AMP/
├── PPMT-AMP.sln                    # Visual Studio Solution
├── src/
│   ├── PPMT-AMP.iOS/               # iOS App Project
│   │   ├── AppDelegate.cs          # App lifecycle management
│   │   ├── LoginViewController.cs  # Login screen
│   │   ├── MainViewController.cs   # Main UI controller
│   │   ├── Main.cs                 # App entry point
│   │   └── Info.plist              # iOS app configuration
│   └── PPMT-AMP.Core/              # Shared Core Library
│       ├── Services/
│       │   ├── ApiClient.cs        # ✅ API Gateway communication
│       │   ├── AuthService.cs      # ✅ Authentication manager
│       │   ├── S3Service.cs        # ❌ (To be removed)
│       │   ├── DynamoDBService.cs  # ❌ (To be removed)
│       │   └── AWSService.cs       # ❌ (To be removed)
│       ├── Models/
│       │   ├── PriceData.cs        # Data models
│       │   └── ApiModels.cs        # API request/response
│       └── Configuration/
│           └── AppConfiguration.cs # App config management
├── lambda/
│   ├── price_query_handler.py     # Query endpoint (deployed)
│   ├── data_scraper.py             # Data scraping (planned)
│   └── price_management_handler.py # CRUD operations (planned)
├── config/
│   ├── appsettings.json            # AWS configuration
│   └── appsettings.production.json.example
├── docs/
│   ├── AWS_ARCHITECTURE.md         # Complete architecture
│   ├── ETL_PIPELINE.md             # Data pipeline details
│   ├── SUPERUSER_MANAGEMENT.md     # Admin portal guide
│   └── SECRETS_MANAGEMENT.md       # Security strategy
├── data/                           # Backend data only (no app access)
│   ├── raw/                        # Scraper output
│   ├── processed/                  # Redshift output
│   └── output/                     # Analytics exports
└── logs/                           # Application logs
```

### Data Flow

**App to Backend (ONLY through API):**
```
iOS App → ApiClient → API Gateway → Lambda → DynamoDB
         (HTTP/HMAC)               (AWS SDK)
```

**Backend ETL Pipeline (No app involvement):**
```
Scraper Lambda → S3 (raw) → Redshift (ETL) → Sync Lambda → DynamoDB
```

**Key Principle:** App never directly accesses S3, DynamoDB, or other AWS services. All operations go through API Gateway.

## Technology Stack

### Frontend (iOS App)
- **Platform**: Xamarin.iOS (.NET 8.0)
- **Language**: C# 12
- **UI Framework**: UIKit (native iOS)
- **Minimum iOS Version**: 13.0

### Backend (AWS Serverless)
- **API**: Amazon API Gateway (REST)
- **Compute**: AWS Lambda (Python 3.11)
- **Database**: Amazon DynamoDB (NoSQL)
- **Authentication**: AWS Cognito (planned)
- **Data Warehouse**: Amazon Redshift (planned)
- **Storage**: Amazon S3 (backend ETL only - no app access)

### Security
- **Request Signing**: HMAC-SHA256 with hybrid method+path payload
- **Rate Limiting**: DynamoDB-backed, 20 requests per 5 minutes
- **Timestamp Validation**: 5-minute window
- **Role-Based Access**: Visitor/User/Superuser roles (planned)

## Prerequisites

### Development Environment
- macOS with Xcode 15+
- Visual Studio for Mac 2022 or Visual Studio Code with C# extension
- .NET 8 SDK
- iOS 13.0+ deployment target

### AWS Resources (Already Deployed)
- ✅ API Gateway: `stou0wlmf4.execute-api.us-east-1.amazonaws.com`
- ✅ Lambda: `ppmt-amp-price-query`
- ✅ DynamoDB: `PPMT-AMP-Prices`, `PPMT-AMP-RateLimits`
- ✅ S3: `ppmt-amp-data-sync-363416481362`, `ppmt-amp-exports-363416481362`

### Future AWS Resources
- 🔄 Cognito User Pool (for authentication)
- 🔄 Redshift Cluster (for ETL)
- 🔄 Additional Lambda functions (scraper, sync, management)

## Installation

### 1. Clone Repository
```bash
git clone https://github.com/BShuGamerSky/PPMT-AMP.git
cd PPMT-AMP
```

### 2. Restore NuGet Packages
```bash
dotnet restore PPMT-AMP.sln
```

### 3. Configuration

The app uses `config/appsettings.json` for AWS endpoints:
```json
{
  "API": {
    "BaseUrl": "https://stou0wlmf4.execute-api.us-east-1.amazonaws.com/prod",
    "AppSecret": "your-secret-key-change-this-in-production"
  },
  "AWS": {
    "Region": "us-east-1"
  }
}
```

**Note**: No AWS credentials needed in the app. All AWS access is through API Gateway.

### 4. Build the Solution
```bash
dotnet build PPMT-AMP.sln
```

## Running the App

### Using Visual Studio for Mac
1. Open `PPMT-AMP.sln`
2. Select iOS Simulator (iPhone 16 Pro recommended)
3. Press **Run** (⌘ + Return)

### Using Command Line
```bash
# Build for iPhone Simulator
dotnet build src/PPMT-AMP.iOS/PPMT-AMP.iOS.csproj -f net8.0-ios

# Run in iOS Simulator (requires Xcode)
# Use Visual Studio for Mac or VS Code with iOS extension
```

### First Launch
1. App shows **Login Screen**
2. Click **"Skip - Continue as Visitor"** for read-only access
3. Click **"Query Prices"** to fetch current market data
4. See rate limit status (20 requests per 5 minutes)

## AWS Services Setup

### S3 Bucket Setup
```bash
# Create S3 bucket
aws s3 mb s3://ppmt-amp-data-bucket

# Set bucket policy (adjust as needed)
aws s3api put-bucket-policy --bucket ppmt-amp-data-bucket --policy file://bucket-policy.json
```

### DynamoDB Table Setup
```bash
# Create DynamoDB table
aws dynamodb create-table \
    --table-name PPMT-AMP-Prices \
    --attribute-definitions \
        AttributeName=Id,AttributeType=S \
        AttributeName=PriceDate,AttributeType=S \
## Usage

### Current Features (v0.1)

#### Query Prices (Visitor Mode)
```
1. Launch app
2. Skip login → "Continue as Visitor"
3. Tap "Query Prices"
4. View product list with market/retail prices
5. Rate limit: 20 requests per 5 minutes
```

#### Sample Response
```json
{
  "products": [
    {
      "Id": "price-001",
      "ProductName": "iPhone 16 Pro",
      "MarketPrice": "1299.00",
      "RetailPrice": "1499.00",
      "Currency": "USD",
      "PriceDate": "2025-12-21"
    }
  ]
}
```

### Planned Features (v0.2+)

#### Superuser Management
- Login with Cognito credentials
- Create new price records
- Edit existing prices
- Delete incorrect entries
- View audit logs

#### Automated Data Pipeline
- Daily scraping from external sources
- Redshift ETL processing
- Automatic sync to DynamoDB

## Development

### Project Structure

- **PPMT-AMP.iOS**: iOS-specific UI and platform code
- **PPMT-AMP.Core**: Shared business logic (ApiClient, AuthService, Models)
- **lambda/**: Backend Lambda functions (Python)
- **docs/**: Architecture and implementation guides
- **config/**: Configuration files (not in version control for production secrets)

### Code Style
- Follow C# naming conventions
- Use async/await for asynchronous operations
- Handle exceptions gracefully with try-catch blocks
- All AWS access via API Gateway (no direct SDK calls from app)

### Important Architecture Principles

1. **API-First**: App communicates only through API Gateway
2. **No Direct AWS Access**: No AWS credentials or SDK in production app
3. **Backend Logic**: All validation, rate limiting, auth in Lambda
4. **S3 Isolation**: S3 used only for backend ETL, never accessed by app
5. **DynamoDB Isolation**: App queries via Lambda, never directly

## Testing

### Manual Testing
1. Test on iOS Simulator (iPhone 16 Pro recommended)
2. Verify API connectivity and rate limiting
3. Test visitor mode queries
4. Check signature validation

### Backend Testing
```bash
# Test Lambda function
aws lambda invoke --function-name ppmt-amp-price-query \
  --payload '{"httpMethod":"GET","path":"/prices"}' \
  output.json
```

## Deployment

### iOS App (Local Development Only)
- Current version for local testing only
- No App Store deployment yet

### Backend Deployment
```bash
# Deploy Lambda function
cd lambda
./deploy.sh

# Update API Gateway (if needed)
# See docs/AWS_ARCHITECTURE.md for details
```

## Documentation

Comprehensive guides available in `docs/`:
- **AWS_ARCHITECTURE.md** - Complete system architecture
- **ETL_PIPELINE.md** - Data pipeline implementation
- **SUPERUSER_MANAGEMENT.md** - Admin portal guide
- **SECRETS_MANAGEMENT.md** - Security best practices
- **AWS_SETUP_GUIDE.md** - Initial AWS setup

## Security

### Current Implementation
- ✅ HMAC-SHA256 request signatures
- ✅ Timestamp validation (5-minute window)
- ✅ Rate limiting (DynamoDB-backed)
- ✅ No AWS credentials in app

### Production Recommendations
- 🔄 Change APP_SECRET from default
- 🔄 Enable Cognito authentication
- 🔄 Set up CloudWatch alarms
- 🔄 Enable AWS CloudTrail
- 🔄 Use AWS Secrets Manager for Lambda secrets

## Troubleshooting

### Common Issues

**"Forbidden" or "Invalid Signature"**
- Check APP_SECRET matches between app and Lambda
- Verify system time is correct (affects timestamp validation)
- Check signature generation logic

**Rate Limit Exceeded**
- Wait 5 minutes for window to reset
- Check rate limit status in app UI
- Superusers exempt (Phase 3)

**Build Errors**
- Clean solution: `dotnet clean`
- Restore packages: `dotnet restore`
- Delete `bin/` and `obj/` folders

## Roadmap

### v0.1 (Current) ✅
- Visitor mode queries
- HMAC security
- Rate limiting
- Basic iOS UI

### v0.2 (Next)
- Automated ETL pipeline
- Redshift data warehouse
- Daily data scraping

### v0.3 (Future)
- Cognito authentication
- Superuser management portal
- Audit logging

### v1.0 (Release)
- CloudWatch monitoring
- Production-ready security
- App Store deployment

## License

[Specify your license here]

## Contact

GitHub: [@BShuGamerSky](https://github.com/BShuGamerSky)

---

**Version**: 0.1.0 (MVP)  
**Last Updated**: December 21, 2025  
**Minimum iOS Version**: 13.0  
**Target Framework**: .NET 8.0

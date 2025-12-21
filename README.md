# PPMT-AMP: After-Market Price Management

A Xamarin.iOS mobile application for managing and analyzing after-market pricing data with AWS backend integration.

## Overview

PPMT-AMP is an iOS mobile app that provides real-time access to after-market pricing data. The app leverages AWS services for data storage, processing, and synchronization, enabling users to track price changes, upload data, and analyze market trends on the go.

## Features

- **AWS Integration**: Full integration with AWS services (S3, DynamoDB, Lambda, Cognito)
- **Data Synchronization**: Real-time sync with cloud-based pricing data
- **File Upload**: Upload pricing data files to AWS S3
- **Database Operations**: Query and update price records in DynamoDB
- **User Authentication**: Secure authentication via AWS Cognito (ready for implementation)
- **Offline Support**: Local caching for offline access (future enhancement)

## Architecture

```
PPMT-AMP/
├── PPMT-AMP.sln                    # Visual Studio Solution
├── src/
│   ├── PPMT-AMP.iOS/               # iOS App Project
│   │   ├── AppDelegate.cs          # App lifecycle management
│   │   ├── MainViewController.cs   # Main UI controller
│   │   ├── Main.cs                 # App entry point
│   │   └── Info.plist              # iOS app configuration
│   └── PPMT-AMP.Core/              # Shared Core Library
│       ├── Services/
│       │   ├── AWSService.cs       # AWS client initialization
│       │   ├── S3Service.cs        # S3 operations
│       │   └── DynamoDBService.cs  # DynamoDB operations
│       ├── Models/
│       │   └── PriceData.cs        # Data models
│       └── Configuration/
│           └── AppConfiguration.cs # App config management
├── config/
│   └── appsettings.json            # AWS and app settings
├── data/                           # Local data storage
│   ├── raw/                        # Raw data files
│   ├── processed/                  # Processed data
│   └── output/                     # Export files
└── logs/                           # Application logs
```

## Technology Stack

- **Platform**: Xamarin.iOS (.NET 7)
- **Backend**: Amazon Web Services (AWS)
  - **AWS S3**: File storage for pricing data
  - **AWS DynamoDB**: NoSQL database for price records
  - **AWS Lambda**: Serverless data processing
  - **AWS Cognito**: User authentication & authorization
- **Language**: C# 10+
- **UI Framework**: UIKit (native iOS)

## Prerequisites

### Development Environment
- macOS with Xcode 14+
- Visual Studio for Mac 2022 or Visual Studio Code with C# extension
- .NET 7 SDK or later
- iOS 11.0+ deployment target

### AWS Setup
1. AWS Account with appropriate permissions
2. AWS CLI configured (optional but recommended)
3. The following AWS resources:
   - S3 bucket for data storage
   - DynamoDB table for price records
   - Cognito User Pool and Identity Pool
   - Lambda functions (optional, for data processing)

## Installation

### 1. Clone or Navigate to Project
```bash
cd /Users/shaofengshu/self-dev
```

### 2. Restore NuGet Packages
```bash
dotnet restore PPMT-AMP.sln
```

### 3. Configure AWS Credentials

Copy the example environment file:
```bash
cp .env.example .env
```

Edit `.env` with your AWS credentials:
```
AWS_REGION=us-east-1
AWS_ACCESS_KEY_ID=your-access-key-id
AWS_SECRET_ACCESS_KEY=your-secret-access-key
S3_BUCKET_NAME=your-bucket-name
DYNAMODB_TABLE_NAME=your-table-name
```

### 4. Update Configuration

Edit `config/appsettings.json` with your AWS resource details:
```json
{
  "AWS": {
    "Region": "us-east-1",
    "S3": {
      "BucketName": "your-bucket-name",
      "DataPath": "market-prices/"
    },
    "DynamoDB": {
      "TableName": "your-table-name",
      "IndexName": "DateIndex"
    }
  }
}
```

### 5. Build the Solution
```bash
dotnet build PPMT-AMP.sln
```

## Running the App

### Using Visual Studio for Mac
1. Open `PPMT-AMP.sln`
2. Select iOS Simulator or connected device
3. Press **Run** (⌘ + Return)

### Using Visual Studio Code
1. Open the project folder
2. Install C# extension
3. Use terminal:
```bash
dotnet build src/PPMT-AMP.iOS/PPMT-AMP.iOS.csproj
```

### Using Command Line
```bash
# Build for iPhone Simulator
dotnet build src/PPMT-AMP.iOS/PPMT-AMP.iOS.csproj -f net7.0-ios -r iossimulator-arm64

# Build for iPhone Device
dotnet build src/PPMT-AMP.iOS/PPMT-AMP.iOS.csproj -f net7.0-ios -r ios-arm64
```

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
    --key-schema \
        AttributeName=Id,KeyType=HASH \
    --global-secondary-indexes \
        "[{\"IndexName\":\"DateIndex\",\"KeySchema\":[{\"AttributeName\":\"PriceDate\",\"KeyType\":\"HASH\"}],\"Projection\":{\"ProjectionType\":\"ALL\"},\"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}}]" \
    --provisioned-throughput \
        ReadCapacityUnits=5,WriteCapacityUnits=5
```

### Cognito Setup
```bash
# Create user pool
aws cognito-idp create-user-pool --pool-name ppmt-amp-users

# Create identity pool
aws cognito-identity create-identity-pool \
    --identity-pool-name ppmt-amp-identity \
    --allow-unauthenticated-identities
```

## Usage

### Main Features

#### 1. Sync Data
- Tap **Sync Data** button to fetch latest pricing data from AWS
- Data is pulled from DynamoDB and cached locally

#### 2. Upload Data
- Tap **Upload Data** button to send data to AWS
- Files are uploaded to S3, metadata saved to DynamoDB

#### 3. View Price History
- Table view displays recent price records
- Pull to refresh for latest data

## Development

### Adding New AWS Services

1. Add NuGet package to project files:
```xml
<PackageReference Include="AWSSDK.NewService" Version="3.7.*" />
```

2. Create service class in `src/PPMT-AMP.Core/Services/`
3. Initialize client in `AWSService.cs`
4. Implement service methods

### Project Structure

- **PPMT-AMP.iOS**: iOS-specific UI and platform code
- **PPMT-AMP.Core**: Shared business logic, models, and AWS services
- **Separation of Concerns**: UI logic separate from business logic

### Code Style
- Follow C# naming conventions
- Use async/await for asynchronous operations
- Handle exceptions gracefully with try-catch blocks
- Log important operations and errors

## Data Pipeline (Future Enhancement)

The app is designed to support data pipeline functionality for:
- Batch processing of price data
- ETL operations from multiple sources
- Data validation and transformation
- Scheduled data synchronization
- Analytics and reporting

Pipeline components can be added to `data/` directory structure.

## Testing

### Unit Tests
```bash
dotnet test
```

### Manual Testing
1. Test on iOS Simulator (various device types)
2. Test on physical iPhone device
3. Test AWS connectivity
4. Test offline scenarios

## Deployment

### App Store Deployment
1. Configure signing certificates in Xcode
2. Update version in `Info.plist`
3. Build release configuration
4. Archive and upload to App Store Connect

### TestFlight Distribution
1. Archive app in Xcode
2. Upload to TestFlight
3. Invite beta testers

## Troubleshooting

### Common Issues

**AWS Connection Fails**
- Verify AWS credentials in `.env`
- Check network connectivity
- Ensure IAM permissions are correct

**Build Errors**
- Clean solution: `dotnet clean`
- Restore packages: `dotnet restore`
- Delete `bin/` and `obj/` folders

**Simulator Issues**
- Reset simulator: Device → Erase All Content and Settings
- Restart Xcode and Visual Studio

## Security Considerations

- **Never commit** `.env` file to version control
- Use AWS Cognito for production authentication
- Implement certificate pinning for API calls
- Enable AWS CloudTrail for audit logging
- Use AWS Secrets Manager for sensitive configuration

## Performance Optimization

- Implement data pagination for large datasets
- Use background tasks for sync operations
- Cache frequently accessed data locally
- Optimize image loading and UI rendering

## Future Enhancements

- [ ] Implement AWS Cognito authentication
- [ ] Add offline mode with local database (SQLite)
- [ ] Implement push notifications via AWS SNS
- [ ] Add data visualization charts
- [ ] Export reports to PDF
- [ ] Multi-language support
- [ ] Dark mode support
- [ ] Apple Watch companion app

## Contributing

1. Create feature branch
2. Make changes
3. Test thoroughly
4. Submit pull request

## License

[Specify your license here]

## Contact

[Add contact information]

## Acknowledgments

- AWS SDK for .NET
- Xamarin.iOS Framework
- [Add other acknowledgments]

---

**Version**: 1.0.0  
**Last Updated**: December 2025  
**Minimum iOS Version**: 11.0

# PPMT-AMP Project

## Project Structure

This Xamarin.iOS mobile application uses AWS backend services for after-market price data management.

## Key Directories

- `src/PPMT-AMP.iOS/` - iOS mobile app UI and platform-specific code
- `src/PPMT-AMP.Core/` - Shared business logic and AWS services
- `config/` - Configuration files
- `data/` - Data storage (raw, processed, output)
- `logs/` - Application logs

## Development Guidelines

- Keep UI code in iOS project
- Keep business logic in Core project
- Use async/await for AWS operations
- Handle all exceptions with proper logging
- Follow C# naming conventions

## AWS Services Used

- S3: File storage
- DynamoDB: NoSQL database
- Lambda: Serverless functions
- Cognito: Authentication

See README.md for full documentation.

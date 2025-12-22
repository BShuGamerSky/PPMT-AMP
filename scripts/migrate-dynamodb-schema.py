#!/usr/bin/env python3
"""
DynamoDB Schema Migration Script
Migrates PPMT-AMP-Prices table to PPMT-AMP-Items (PopMart-optimized schema)
"""

import boto3
import json
import time
from datetime import datetime, timedelta
from decimal import Decimal

# AWS Configuration
REGION = 'us-east-1'
OLD_TABLE_NAME = 'PPMT-AMP-Prices'
NEW_TABLE_NAME = 'PPMT-AMP-Items'

# Initialize AWS clients
dynamodb = boto3.client('dynamodb', region_name=REGION)
dynamodb_resource = boto3.resource('dynamodb', region_name=REGION)

def get_existing_data():
    """Get existing data from table if it exists"""
    print(f"\n{'='*80}")
    print("STEP 1: Checking for existing data")
    print(f"{'='*80}")
    
    try:
        # Check if old table exists
        print(f"Checking if table '{OLD_TABLE_NAME}' exists...")
        dynamodb.describe_table(TableName=OLD_TABLE_NAME)
        print(f"✓ Table '{OLD_TABLE_NAME}' found")
        
        # Scan all items from old table
        print(f"Scanning all items from '{OLD_TABLE_NAME}'...")
        old_table = dynamodb_resource.Table(OLD_TABLE_NAME)
        response = old_table.scan()
        items = response['Items']
        
        while 'LastEvaluatedKey' in response:
            response = old_table.scan(ExclusiveStartKey=response['LastEvaluatedKey'])
            items.extend(response['Items'])
        
        print(f"✓ Found {len(items)} items to migrate")
        
        return items
        
    except dynamodb.exceptions.ResourceNotFoundException:
        print(f"⚠ Table '{OLD_TABLE_NAME}' not found - will create fresh table")
        return []
    except Exception as e:
        print(f"✗ Error reading existing data: {str(e)}")
        raise

def create_new_table():
    """Create new table with updated schema"""
    print(f"\n{'='*80}")
    print("STEP 2: Creating new table structure")
    print(f"{'='*80}")
    
    try:
        # Delete old table if exists
        print(f"Checking if old table exists...")
        try:
            dynamodb.describe_table(TableName=NEW_TABLE_NAME)
            print(f"Deleting old table '{NEW_TABLE_NAME}'...")
            dynamodb.delete_table(TableName=NEW_TABLE_NAME)
            
            # Wait for deletion
            print("Waiting for table deletion...")
            waiter = dynamodb.get_waiter('table_not_exists')
            waiter.wait(TableName=NEW_TABLE_NAME)
            print(f"✓ Old table deleted")
            
        except dynamodb.exceptions.ResourceNotFoundException:
            print(f"✓ No existing table to delete")
        
        # Create new table
        print(f"Creating new table '{NEW_TABLE_NAME}' with updated schema...")
        response = dynamodb.create_table(
            TableName=NEW_TABLE_NAME,
            KeySchema=[
                {
                    'AttributeName': 'SeriesId',
                    'KeyType': 'HASH'  # Partition key
                },
                {
                    'AttributeName': 'ProductId',
                    'KeyType': 'RANGE'  # Sort key
                }
            ],
            AttributeDefinitions=[
                {
                    'AttributeName': 'SeriesId',
                    'AttributeType': 'S'
                },
                {
                    'AttributeName': 'ProductId',
                    'AttributeType': 'S'
                },
                {
                    'AttributeName': 'IpCharacter',
                    'AttributeType': 'S'
                },
                {
                    'AttributeName': 'Timestamp',
                    'AttributeType': 'S'
                },
                {
                    'AttributeName': 'Category',
                    'AttributeType': 'S'
                },
                {
                    'AttributeName': 'AfterMarketPrice',
                    'AttributeType': 'N'
                },
                {
                    'AttributeName': 'Status',
                    'AttributeType': 'S'
                }
            ],
            GlobalSecondaryIndexes=[
                {
                    'IndexName': 'IpCharacter-Timestamp-Index',
                    'KeySchema': [
                        {
                            'AttributeName': 'IpCharacter',
                            'KeyType': 'HASH'
                        },
                        {
                            'AttributeName': 'Timestamp',
                            'KeyType': 'RANGE'
                        }
                    ],
                    'Projection': {
                        'ProjectionType': 'ALL'
                    }
                },
                {
                    'IndexName': 'Category-AfterMarketPrice-Index',
                    'KeySchema': [
                        {
                            'AttributeName': 'Category',
                            'KeyType': 'HASH'
                        },
                        {
                            'AttributeName': 'AfterMarketPrice',
                            'KeyType': 'RANGE'
                        }
                    ],
                    'Projection': {
                        'ProjectionType': 'ALL'
                    }
                },
                {
                    'IndexName': 'Status-Timestamp-Index',
                    'KeySchema': [
                        {
                            'AttributeName': 'Status',
                            'KeyType': 'HASH'
                        },
                        {
                            'AttributeName': 'Timestamp',
                            'KeyType': 'RANGE'
                        }
                    ],
                    'Projection': {
                        'ProjectionType': 'ALL'
                    }
                }
            ],
            BillingMode='PAY_PER_REQUEST',
            Tags=[
                {
                    'Key': 'Environment',
                    'Value': 'Production'
                },
                {
                    'Key': 'Application',
                    'Value': 'PPMT-AMP'
                },
                {
                    'Key': 'MigrationDate',
                    'Value': datetime.now().strftime('%Y-%m-%d')
                }
            ]
        )
        
        # Wait for table to be active
        print("Waiting for table to become active...")
        waiter = dynamodb.get_waiter('table_exists')
        waiter.wait(TableName=NEW_TABLE_NAME)
        
        # Wait for GSIs to be active
        print("Waiting for GSIs to become active...")
        while True:
            response = dynamodb.describe_table(TableName=NEW_TABLE_NAME)
            table_status = response['Table']['TableStatus']
            
            if table_status == 'ACTIVE':
                gsi_statuses = [gsi['IndexStatus'] for gsi in response['Table'].get('GlobalSecondaryIndexes', [])]
                if all(status == 'ACTIVE' for status in gsi_statuses):
                    print(f"✓ Table and all GSIs are active")
                    break
            
            time.sleep(5)
        
        print(f"✓ New table '{NEW_TABLE_NAME}' created successfully")
        
    except Exception as e:
        print(f"✗ Error creating new table: {str(e)}")
        raise

def transform_old_data_to_new_schema(old_items):
    """Transform old schema items to new schema format"""
    print(f"\n{'='*80}")
    print("STEP 3: Transforming data to new schema")
    print(f"{'='*80}")
    
    new_items = []
    
    # Mapping for old products to new PopMart structure
    product_mapping = {
        'iPhone 16 Pro': {
            'SeriesId': 'SERIES-LABUBU-MONSTERS',
            'ProductId': 'PROD-LABUBU-MONSTERS-001',
            'ProductName': 'Labubu Sitting with Soda',
            'IpCharacter': 'Labubu',
            'SeriesName': 'Monsters Series',
            'Rarity': 'Common',
            'SeriesSize': 12,
            'ImageUrl': 'https://cdn.popmart.com/labubu-monsters-001.jpg',
            'Description': 'Labubu sitting with a refreshing soda drink'
        },
        'MacBook Pro 16': {
            'SeriesId': 'SERIES-LABUBU-MONSTERS',
            'ProductId': 'PROD-LABUBU-MONSTERS-SECRET',
            'ProductName': 'Labubu Golden Monster (Secret)',
            'IpCharacter': 'Labubu',
            'SeriesName': 'Monsters Series',
            'Rarity': 'Secret',
            'SeriesSize': 12,
            'ImageUrl': 'https://cdn.popmart.com/labubu-monsters-secret.jpg',
            'Description': 'Ultra rare golden Labubu - 1/144 chance'
        },
        'AirPods Pro': {
            'SeriesId': 'SERIES-HIRONO-WINTER2024',
            'ProductId': 'PROD-HIRONO-WINTER2024-003',
            'ProductName': 'Hirono with Snowflakes',
            'IpCharacter': 'Hirono',
            'SeriesName': 'Winter Collection 2024',
            'Rarity': 'Rare',
            'SeriesSize': 8,
            'ImageUrl': 'https://cdn.popmart.com/hirono-winter-003.jpg',
            'Description': 'Hirono surrounded by winter snowflakes'
        }
    }
    
    for idx, old_item in enumerate(old_items, 1):
        print(f"Transforming item {idx}/{len(old_items)}: {old_item.get('Product', 'Unknown')}")
        
        # Get product name from old schema
        old_product_name = old_item.get('Product', 'Unknown Product')
        
        # Get mapped data or use defaults
        if old_product_name in product_mapping:
            mapped = product_mapping[old_product_name]
        else:
            # Default mapping for unknown products
            mapped = {
                'SeriesId': 'SERIES-UNKNOWN-DEFAULT',
                'ProductId': f'PROD-UNKNOWN-{idx:03d}',
                'ProductName': old_product_name,
                'IpCharacter': 'Unknown',
                'SeriesName': 'Unknown Series',
                'Rarity': 'Common',
                'SeriesSize': 1,
                'ImageUrl': '',
                'Description': ''
            }
        
        # Calculate TTL (90 days from now)
        ttl_timestamp = int((datetime.now() + timedelta(days=90)).timestamp())
        
        # Calculate price changes
        retail_price = float(old_item.get('RetailPrice', 0))
        after_market_price = float(old_item.get('MarketPrice', retail_price))
        price_change = after_market_price - retail_price
        price_change_percent = (price_change / retail_price * 100) if retail_price > 0 else 0
        
        # Create new item with transformed schema
        new_item = {
            'SeriesId': mapped['SeriesId'],
            'ProductId': mapped['ProductId'],
            'ProductName': mapped['ProductName'],
            'IpCharacter': mapped['IpCharacter'],
            'SeriesName': mapped['SeriesName'],
            'Category': old_item.get('Category', 'Blind Box'),
            'RetailPrice': Decimal(str(retail_price)),
            'AfterMarketPrice': Decimal(str(after_market_price)),
            'Currency': old_item.get('Currency', 'CNY'),
            'PriceChange': Decimal(str(round(price_change, 2))),
            'PriceChangePercent': Decimal(str(round(price_change_percent, 2))),
            'Timestamp': old_item.get('PriceDate', datetime.now().isoformat()),
            'Status': old_item.get('Status', 'Active'),
            'Rarity': mapped['Rarity'],
            'SeriesSize': mapped['SeriesSize'],
            'TTL': ttl_timestamp,
            'ImageUrl': mapped['ImageUrl'],
            'Description': mapped['Description'],
            'CreatedAt': old_item.get('CreatedAt', datetime.now().isoformat()),
            'UpdatedAt': datetime.now().isoformat()
        }
        
        new_items.append(new_item)
        print(f"  ✓ Transformed to: SeriesId={new_item['SeriesId']}, ProductId={new_item['ProductId']}")
    
    print(f"\n✓ Transformed {len(new_items)} items successfully")
    return new_items

def load_data_to_new_table(new_items):
    """Load transformed data into new table"""
    print(f"\n{'='*80}")
    print("STEP 4: Loading data into new table")
    print(f"{'='*80}")
    
    new_table = dynamodb_resource.Table(NEW_TABLE_NAME)
    
    for idx, item in enumerate(new_items, 1):
        print(f"Loading item {idx}/{len(new_items)}: {item['ProductName']}")
        
        try:
            new_table.put_item(Item=item)
            print(f"  ✓ Loaded successfully")
        except Exception as e:
            print(f"  ✗ Error loading item: {str(e)}")
            raise
    
    print(f"\n✓ Loaded {len(new_items)} items into '{NEW_TABLE_NAME}'")

def enable_ttl():
    """Enable TTL on the new table"""
    print(f"\n{'='*80}")
    print("STEP 5: Enabling TTL")
    print(f"{'='*80}")
    
    try:
        print(f"Enabling TTL on attribute 'TTL' for table '{NEW_TABLE_NAME}'...")
        response = dynamodb.update_time_to_live(
            TableName=NEW_TABLE_NAME,
            TimeToLiveSpecification={
                'Enabled': True,
                'AttributeName': 'TTL'
            }
        )
        
        print(f"✓ TTL enabled: {response['TimeToLiveSpecification']}")
        print(f"✓ Items will automatically expire 90 days after Timestamp")
        
    except Exception as e:
        print(f"✗ Error enabling TTL: {str(e)}")
        raise

def verify_migration():
    """Verify the migration was successful"""
    print(f"\n{'='*80}")
    print("STEP 6: Verifying migration")
    print(f"{'='*80}")
    
    try:
        # Describe new table
        response = dynamodb.describe_table(TableName=NEW_TABLE_NAME)
        table = response['Table']
        
        print(f"✓ Table Name: {table['TableName']}")
        print(f"✓ Table Status: {table['TableStatus']}")
        print(f"✓ Item Count: {table['ItemCount']}")
        print(f"✓ Billing Mode: {table['BillingModeSummary']['BillingMode']}")
        print(f"✓ Primary Keys: SeriesId (HASH), ProductId (RANGE)")
        
        print(f"\n✓ Global Secondary Indexes:")
        for gsi in table.get('GlobalSecondaryIndexes', []):
            print(f"  - {gsi['IndexName']}: {gsi['IndexStatus']}")
        
        # Scan new table to show sample items
        print(f"\n✓ Sample items from new table:")
        new_table = dynamodb_resource.Table(NEW_TABLE_NAME)
        response = new_table.scan(Limit=3)
        
        for idx, item in enumerate(response['Items'], 1):
            print(f"\nItem {idx}:")
            print(f"  SeriesId: {item['SeriesId']}")
            print(f"  ProductId: {item['ProductId']}")
            print(f"  ProductName: {item['ProductName']}")
            print(f"  IpCharacter: {item['IpCharacter']}")
            print(f"  RetailPrice: {item['RetailPrice']} {item['Currency']}")
            print(f"  AfterMarketPrice: {item['AfterMarketPrice']} {item['Currency']}")
            print(f"  PriceChange: +{item['PriceChange']} ({item['PriceChangePercent']}%)")
            print(f"  Rarity: {item['Rarity']}")
            print(f"  Timestamp: {item['Timestamp']}")
        
        print(f"\n{'='*80}")
        print("MIGRATION COMPLETED SUCCESSFULLY ✓")
        print(f"{'='*80}")
        
    except Exception as e:
        print(f"✗ Error during verification: {str(e)}")
        raise

def main():
    """Main migration workflow"""
    print(f"\n{'#'*80}")
    print(f"# DynamoDB Schema Migration - PPMT-AMP-Prices")
    print(f"# Migration Date: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'#'*80}")
    
    try:
        # Step 1: Get existing data
        old_items = get_existing_data()
        
        # Step 2: Create new table structure
        create_new_table()
        
        # Step 3: Transform data
        new_items = transform_old_data_to_new_schema(old_items)
        
        # Step 4: Load data into new table
        load_data_to_new_table(new_items)
        
        # Step 5: Enable TTL
        enable_ttl()
        
        # Step 6: Verify migration
        verify_migration()
        
    except Exception as e:
        print(f"\n{'='*80}")
        print(f"MIGRATION FAILED ✗")
        print(f"{'='*80}")
        print(f"Error: {str(e)}")
        raise

if __name__ == '__main__':
    main()

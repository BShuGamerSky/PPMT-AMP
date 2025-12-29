#!/usr/bin/env python3
"""
Populate DynamoDB with dummy PopMart product data for testing
"""

import boto3
import json
from datetime import datetime

# Initialize DynamoDB client
dynamodb = boto3.resource('dynamodb', region_name='us-east-1')
items_table = dynamodb.Table('PPMT-AMP-Items')
series_table = dynamodb.Table('PPMT-AMP-Series')

# Dummy product data for popular PopMart series
dummy_items = [
    # Labubu The Monsters Series
    {
        'SeriesId': 'SERIES-LABUBU-001',
        'ProductId': 'PROD-LABUBU-001',
        'ProductName': 'Labubu The Monsters - Red Devil',
        'IpCharacter': 'Labubu',
        'SeriesName': 'The Monsters',
        'Rarity': 'Secret',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 599,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-LABUBU-001',
        'ProductId': 'PROD-LABUBU-002',
        'ProductName': 'Labubu The Monsters - Blue Fairy',
        'IpCharacter': 'Labubu',
        'SeriesName': 'The Monsters',
        'Rarity': 'Rare',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 299,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-LABUBU-001',
        'ProductId': 'PROD-LABUBU-003',
        'ProductName': 'Labubu The Monsters - Pink Angel',
        'IpCharacter': 'Labubu',
        'SeriesName': 'The Monsters',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 129,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-LABUBU-001',
        'ProductId': 'PROD-LABUBU-004',
        'ProductName': 'Labubu The Monsters - Green Dragon',
        'IpCharacter': 'Labubu',
        'SeriesName': 'The Monsters',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 99,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-LABUBU-001',
        'ProductId': 'PROD-LABUBU-005',
        'ProductName': 'Labubu The Monsters - Yellow Duck',
        'IpCharacter': 'Labubu',
        'SeriesName': 'The Monsters',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 89,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-LABUBU-001',
        'ProductId': 'PROD-LABUBU-006',
        'ProductName': 'Labubu The Monsters - Purple Ghost',
        'IpCharacter': 'Labubu',
        'SeriesName': 'The Monsters',
        'Rarity': 'Rare',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 259,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    
    # Hirono Winter Collection
    {
        'SeriesId': 'SERIES-HIRONO-001',
        'ProductId': 'PROD-HIRONO-001',
        'ProductName': 'Hirono Winter Collection - Snowflake',
        'IpCharacter': 'Hirono',
        'SeriesName': 'Winter Collection',
        'Rarity': 'Secret',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 499,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-HIRONO-001',
        'ProductId': 'PROD-HIRONO-002',
        'ProductName': 'Hirono Winter Collection - Igloo',
        'IpCharacter': 'Hirono',
        'SeriesName': 'Winter Collection',
        'Rarity': 'Rare',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 199,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-HIRONO-001',
        'ProductId': 'PROD-HIRONO-003',
        'ProductName': 'Hirono Winter Collection - Scarf',
        'IpCharacter': 'Hirono',
        'SeriesName': 'Winter Collection',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 119,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-HIRONO-001',
        'ProductId': 'PROD-HIRONO-004',
        'ProductName': 'Hirono Winter Collection - Mittens',
        'IpCharacter': 'Hirono',
        'SeriesName': 'Winter Collection',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 99,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-HIRONO-001',
        'ProductId': 'PROD-HIRONO-005',
        'ProductName': 'Hirono Winter Collection - Hot Cocoa',
        'IpCharacter': 'Hirono',
        'SeriesName': 'Winter Collection',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 109,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    
    # Molly Forest Fantasy Series
    {
        'SeriesId': 'SERIES-MOLLY-001',
        'ProductId': 'PROD-MOLLY-001',
        'ProductName': 'Molly Forest Fantasy - Mushroom Queen',
        'IpCharacter': 'Molly',
        'SeriesName': 'Forest Fantasy',
        'Rarity': 'Secret',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 559,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-MOLLY-001',
        'ProductId': 'PROD-MOLLY-002',
        'ProductName': 'Molly Forest Fantasy - Fairy Wings',
        'IpCharacter': 'Molly',
        'SeriesName': 'Forest Fantasy',
        'Rarity': 'Rare',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 279,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-MOLLY-001',
        'ProductId': 'PROD-MOLLY-003',
        'ProductName': 'Molly Forest Fantasy - Flower Crown',
        'IpCharacter': 'Molly',
        'SeriesName': 'Forest Fantasy',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 129,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-MOLLY-001',
        'ProductId': 'PROD-MOLLY-004',
        'ProductName': 'Molly Forest Fantasy - Butterfly Friend',
        'IpCharacter': 'Molly',
        'SeriesName': 'Forest Fantasy',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 99,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-MOLLY-001',
        'ProductId': 'PROD-MOLLY-005',
        'ProductName': 'Molly Forest Fantasy - Tree Spirit',
        'IpCharacter': 'Molly',
        'SeriesName': 'Forest Fantasy',
        'Rarity': 'Rare',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 229,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    
    # Skullpanda City Night Series
    {
        'SeriesId': 'SERIES-SKULL-001',
        'ProductId': 'PROD-SKULL-001',
        'ProductName': 'Skullpanda City Night - Neon Dreams',
        'IpCharacter': 'Skullpanda',
        'SeriesName': 'City Night',
        'Rarity': 'Secret',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 699,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-SKULL-001',
        'ProductId': 'PROD-SKULL-002',
        'ProductName': 'Skullpanda City Night - Midnight Run',
        'IpCharacter': 'Skullpanda',
        'SeriesName': 'City Night',
        'Rarity': 'Rare',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 319,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-SKULL-001',
        'ProductId': 'PROD-SKULL-003',
        'ProductName': 'Skullpanda City Night - Street Light',
        'IpCharacter': 'Skullpanda',
        'SeriesName': 'City Night',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 139,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-SKULL-001',
        'ProductId': 'PROD-SKULL-004',
        'ProductName': 'Skullpanda City Night - Taxi Ride',
        'IpCharacter': 'Skullpanda',
        'SeriesName': 'City Night',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 109,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    },
    {
        'SeriesId': 'SERIES-SKULL-001',
        'ProductId': 'PROD-SKULL-005',
        'ProductName': 'Skullpanda City Night - Urban Explorer',
        'IpCharacter': 'Skullpanda',
        'SeriesName': 'City Night',
        'Rarity': 'Common',
        'Category': 'Blind Box',
        'RetailPrice': 69,
        'AfterMarketPrice': 119,
        'Status': 'Available',
        'UpdatedAt': datetime.now().isoformat()
    }
]

# Series metadata
dummy_series = [
    # Labubu series
    {
        'SeriesId': 'SERIES-LABUBU-001',
        'SeriesName': 'The Monsters',
        'IpCharacter': 'Labubu',
        'ReleaseDate': '2024-01',
        'TotalItems': 12,
        'RelatedIpCharacters': ['Labubu'],
        'Status': 'Active',
        'RetailPrice': 69,
        'Currency': 'CNY'
    },
    {
        'SeriesId': 'SERIES-LABUBU-002',
        'SeriesName': 'Little Mischief',
        'IpCharacter': 'Labubu',
        'ReleaseDate': '2024-06',
        'TotalItems': 10,
        'RelatedIpCharacters': ['Labubu'],
        'Status': 'Active',
        'RetailPrice': 69,
        'Currency': 'CNY'
    },
    # Hirono series
    {
        'SeriesId': 'SERIES-HIRONO-001',
        'SeriesName': 'Winter Collection',
        'IpCharacter': 'Hirono',
        'ReleaseDate': '2023-12',
        'TotalItems': 8,
        'RelatedIpCharacters': ['Hirono'],
        'Status': 'Active',
        'RetailPrice': 69,
        'Currency': 'CNY'
    },
    {
        'SeriesId': 'SERIES-HIRONO-002',
        'SeriesName': 'The Other One',
        'IpCharacter': 'Hirono',
        'ReleaseDate': '2024-03',
        'TotalItems': 9,
        'RelatedIpCharacters': ['Hirono'],
        'Status': 'Active',
        'RetailPrice': 69,
        'Currency': 'CNY'
    },
    {
        'SeriesId': 'SERIES-HIRONO-003',
        'SeriesName': 'Little Princess',
        'IpCharacter': 'Hirono',
        'ReleaseDate': '2024-08',
        'TotalItems': 10,
        'RelatedIpCharacters': ['Hirono'],
        'Status': 'Pre-Order',
        'RetailPrice': 69,
        'Currency': 'CNY'
    },
    # Molly series
    {
        'SeriesId': 'SERIES-MOLLY-001',
        'SeriesName': 'Forest Fantasy',
        'IpCharacter': 'Molly',
        'ReleaseDate': '2024-02',
        'TotalItems': 10,
        'RelatedIpCharacters': ['Molly'],
        'Status': 'Active',
        'RetailPrice': 69,
        'Currency': 'CNY'
    },
    {
        'SeriesId': 'SERIES-MOLLY-002',
        'SeriesName': 'Reshape',
        'IpCharacter': 'Molly',
        'ReleaseDate': '2024-05',
        'TotalItems': 12,
        'RelatedIpCharacters': ['Molly'],
        'Status': 'Active',
        'RetailPrice': 69,
        'Currency': 'CNY'
    },
    # Skullpanda series
    {
        'SeriesId': 'SERIES-SKULL-001',
        'SeriesName': 'City Night',
        'IpCharacter': 'Skullpanda',
        'ReleaseDate': '2024-04',
        'TotalItems': 9,
        'RelatedIpCharacters': ['Skullpanda'],
        'Status': 'Active',
        'RetailPrice': 69,
        'Currency': 'CNY'
    }
]

def populate_items():
    """Populate items table with dummy data"""
    print("Populating PPMT-AMP-Items table...")
    
    for item in dummy_items:
        try:
            items_table.put_item(Item=item)
            print(f"✓ Added: {item['ProductName']}")
        except Exception as e:
            print(f"✗ Failed to add {item['ProductName']}: {str(e)}")
    
    print(f"\n✅ Successfully added {len(dummy_items)} items to PPMT-AMP-Items")

def populate_series():
    """Populate series table with dummy data"""
    print("\nPopulating PPMT-AMP-Series table...")
    
    for series in dummy_series:
        try:
            series_table.put_item(Item=series)
            print(f"✓ Added: {series['SeriesName']} ({series['IpCharacter']})")
        except Exception as e:
            print(f"✗ Failed to add {series['SeriesName']}: {str(e)}")
    
    print(f"\n✅ Successfully added {len(dummy_series)} series to PPMT-AMP-Series")

def main():
    print("=" * 60)
    print("PopMart Dummy Data Population Script")
    print("=" * 60)
    print()
    
    populate_items()
    populate_series()
    
    print("\n" + "=" * 60)
    print("✅ All dummy data has been populated!")
    print("=" * 60)
    print("\nYou can now:")
    print("1. Launch the iOS app and pull to refresh")
    print("2. Test the search and filter functionality")
    print("3. View product details by tapping on items")

if __name__ == '__main__':
    main()

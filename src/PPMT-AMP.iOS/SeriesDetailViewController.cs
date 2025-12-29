using System;
using System.Collections.Generic;
using System.Linq;
using UIKit;
using CoreGraphics;
using PPMT_AMP.Core.Services;
using PPMT_AMP.Core.Models;

namespace PPMT_AMP.iOS;

public class SeriesDetailViewController : UIViewController
{
    private UISegmentedControl? viewModeControl;
    private UITableView? itemsTableView;
    private UICollectionView? itemsCollectionView;
    private UIActivityIndicatorView? activityIndicator;
    private UIRefreshControl? refreshControl;
    private UILabel? emptyStateLabel;
    private UILabel? seriesHeaderLabel;

    private ApiClient apiClient;
    private PpmtSeries series;
    private List<PpmtItem> seriesItems = new();
    private bool isGridView = false;

    public SeriesDetailViewController(PpmtSeries series)
    {
        this.series = series;
        apiClient = ApiClient.Instance;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        Title = series.SeriesName;
        
        if (View != null)
        {
            View.BackgroundColor = UIColor.FromRGB(245, 245, 250);
            SetupUI();
            LoadSeriesItems();
        }
    }

    private void SetupUI()
    {
        if (View == null) return;

        nfloat yOffset = 100;

        // Series header info
        seriesHeaderLabel = new UILabel
        {
            Frame = new CGRect(16, yOffset, View.Bounds.Width - 32, 60),
            Font = UIFont.SystemFontOfSize(14, UIFontWeight.Regular),
            TextColor = UIColor.FromRGB(99, 99, 102),
            Lines = 0
        };
        string headerText = $"IP Character: {series.IpCharacter}\nRelease Date: {series.ReleaseDate ?? "TBA"}\nTotal Items: {series.TotalItems}";
        seriesHeaderLabel.Text = headerText;
        View.AddSubview(seriesHeaderLabel);
        yOffset += 70;

        // View mode toggle (List/Grid)
        viewModeControl = new UISegmentedControl(new string[] { "List", "Grid" })
        {
            Frame = new CGRect(16, yOffset, View.Bounds.Width - 32, 32),
            SelectedSegment = 0
        };
        viewModeControl.SelectedSegmentTintColor = UIColor.FromRGB(255, 69, 58);
        viewModeControl.ValueChanged += ViewModeControl_ValueChanged;
        View.AddSubview(viewModeControl);
        yOffset += 42;

        // Table View for List mode
        itemsTableView = new UITableView(new CGRect(0, yOffset, View.Bounds.Width, View.Bounds.Height - yOffset), UITableViewStyle.Plain)
        {
            BackgroundColor = UIColor.FromRGB(245, 245, 250),
            SeparatorStyle = UITableViewCellSeparatorStyle.None,
            ContentInset = new UIEdgeInsets(8, 0, 8, 0)
        };
        itemsTableView.Source = new ItemsTableSource(this);
        
        refreshControl = new UIRefreshControl();
        refreshControl.TintColor = UIColor.FromRGB(255, 69, 58);
        refreshControl.ValueChanged += RefreshControl_ValueChanged;
        itemsTableView.RefreshControl = refreshControl;
        
        View.AddSubview(itemsTableView);

        // Collection View for Grid mode
        var layout = new UICollectionViewFlowLayout
        {
            ScrollDirection = UICollectionViewScrollDirection.Vertical,
            MinimumInteritemSpacing = 12,
            MinimumLineSpacing = 12,
            SectionInset = new UIEdgeInsets(12, 12, 12, 12)
        };
        
        nfloat gridWidth = (View.Bounds.Width - 36) / 2;
        layout.ItemSize = new CGSize(gridWidth, gridWidth * 1.4f);
        
        itemsCollectionView = new UICollectionView(new CGRect(0, yOffset, View.Bounds.Width, View.Bounds.Height - yOffset), layout)
        {
            BackgroundColor = UIColor.FromRGB(245, 245, 250),
            Hidden = true
        };
        itemsCollectionView.RegisterClassForCell(typeof(ItemGridCell), "ItemCell");
        itemsCollectionView.Source = new ItemsCollectionSource(this);
        View.AddSubview(itemsCollectionView);

        // Empty state
        emptyStateLabel = new UILabel
        {
            Frame = new CGRect(40, View.Bounds.Height / 2 - 60, View.Bounds.Width - 80, 120),
            Text = "No items found in this series\n\nPull down to refresh",
            Font = UIFont.SystemFontOfSize(17, UIFontWeight.Regular),
            TextColor = UIColor.FromRGB(142, 142, 147),
            TextAlignment = UITextAlignment.Center,
            Lines = 0,
            Hidden = true
        };
        View.AddSubview(emptyStateLabel);

        // Activity Indicator
        activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
        {
            Center = View.Center,
            HidesWhenStopped = true,
            Color = UIColor.FromRGB(255, 69, 58)
        };
        View.AddSubview(activityIndicator);
    }

    private async void LoadSeriesItems()
    {
        activityIndicator?.StartAnimating();
        emptyStateLabel!.Hidden = true;

        try
        {
            // Query all items for this series
            var response = await apiClient.QueryPricesAsync(series.SeriesId);
            
            if (response.Success && response.Data != null)
            {
                seriesItems = response.Data;
                itemsTableView?.ReloadData();
                itemsCollectionView?.ReloadData();
                
                if (seriesItems.Count == 0)
                {
                    emptyStateLabel!.Hidden = false;
                }
            }
            else
            {
                ShowAlert("Load Failed", response.Message ?? "Failed to load items");
                emptyStateLabel!.Hidden = false;
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Failed to load items: {ex.Message}");
            emptyStateLabel!.Hidden = false;
        }
        finally
        {
            activityIndicator?.StopAnimating();
        }
    }

    private void ViewModeControl_ValueChanged(object? sender, EventArgs e)
    {
        isGridView = viewModeControl?.SelectedSegment == 1;
        itemsTableView!.Hidden = isGridView;
        itemsCollectionView!.Hidden = !isGridView;
    }

    private async void RefreshControl_ValueChanged(object? sender, EventArgs e)
    {
        try
        {
            var response = await apiClient.QueryPricesAsync(series.SeriesId);
            
            if (response.Success && response.Data != null)
            {
                seriesItems = response.Data;
                itemsTableView?.ReloadData();
                itemsCollectionView?.ReloadData();
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Failed to refresh: {ex.Message}");
        }
        finally
        {
            refreshControl?.EndRefreshing();
        }
    }

    private void ShowAlert(string title, string message)
    {
        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
        PresentViewController(alert, true, null);
    }

    // Table Source for List View
    private class ItemsTableSource : UITableViewSource
    {
        private SeriesDetailViewController parent;
        private const string cellIdentifier = "ItemCell";

        public ItemsTableSource(SeriesDetailViewController parent)
        {
            this.parent = parent;
        }

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return parent.seriesItems.Count;
        }

        public override UITableViewCell GetCell(UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell(cellIdentifier) ?? new UITableViewCell(UITableViewCellStyle.Subtitle, cellIdentifier);
            
            var item = parent.seriesItems[indexPath.Row];

            cell.BackgroundColor = UIColor.White;
            cell.Layer.CornerRadius = 12;
            cell.Layer.MasksToBounds = true;
            cell.ContentView.BackgroundColor = UIColor.White;

            // Rarity emoji
            string rarityEmoji = item.Rarity switch
            {
                "Secret" => "💎",
                "Rare" => "⭐",
                _ => "🎁"
            };

            cell.TextLabel!.Text = $"{rarityEmoji} {item.ProductName}";
            cell.TextLabel.Font = UIFont.SystemFontOfSize(16, UIFontWeight.Semibold);
            cell.TextLabel.TextColor = UIColor.FromRGB(28, 28, 30);

            string priceInfo = $"Retail: ¥{item.RetailPrice:N0} → Market: ¥{item.AfterMarketPrice:N0}";
            decimal priceDiff = item.AfterMarketPrice - item.RetailPrice;
            decimal percentChange = (priceDiff / item.RetailPrice) * 100;
            
            string changeIndicator = priceDiff > 0 ? $"↑ +{percentChange:F0}%" : "→";
            
            cell.DetailTextLabel!.Text = $"{priceInfo} {changeIndicator}";
            cell.DetailTextLabel.Font = UIFont.SystemFontOfSize(14, UIFontWeight.Regular);
            cell.DetailTextLabel.TextColor = priceDiff > 0 ? UIColor.FromRGB(52, 199, 89) : UIColor.FromRGB(142, 142, 147);

            cell.Accessory = UITableViewCellAccessory.None;
            
            return cell;
        }

        public override nfloat GetHeightForRow(UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            return 88;
        }

        public override void RowSelected(UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);
            // Could navigate to item detail view in future
        }
    }

    // Collection Source for Grid View
    private class ItemsCollectionSource : UICollectionViewSource
    {
        private SeriesDetailViewController parent;

        public ItemsCollectionSource(SeriesDetailViewController parent)
        {
            this.parent = parent;
        }

        public override nint GetItemsCount(UICollectionView collectionView, nint section)
        {
            return parent.seriesItems.Count;
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, Foundation.NSIndexPath indexPath)
        {
            var cell = (ItemGridCell)collectionView.DequeueReusableCell("ItemCell", indexPath);
            var item = parent.seriesItems[indexPath.Row];
            cell.UpdateCell(item);
            return cell;
        }

        public override void ItemSelected(UICollectionView collectionView, Foundation.NSIndexPath indexPath)
        {
            // Could navigate to item detail view in future
        }
    }

    // Grid Cell
    private class ItemGridCell : UICollectionViewCell
    {
        private UILabel? nameLabel;
        private UILabel? rarityLabel;
        private UILabel? retailPriceLabel;
        private UILabel? marketPriceLabel;

        public ItemGridCell(IntPtr handle) : base(handle)
        {
            SetupCell();
        }

        private void SetupCell()
        {
            ContentView.BackgroundColor = UIColor.White;
            ContentView.Layer.CornerRadius = 12;
            ContentView.Layer.MasksToBounds = true;

            nameLabel = new UILabel
            {
                Frame = new CGRect(12, 12, ContentView.Bounds.Width - 24, 50),
                Font = UIFont.SystemFontOfSize(14, UIFontWeight.Semibold),
                TextColor = UIColor.FromRGB(28, 28, 30),
                Lines = 2
            };
            ContentView.AddSubview(nameLabel);

            rarityLabel = new UILabel
            {
                Frame = new CGRect(12, 70, ContentView.Bounds.Width - 24, 20),
                Font = UIFont.SystemFontOfSize(12, UIFontWeight.Regular),
                TextColor = UIColor.FromRGB(142, 142, 147)
            };
            ContentView.AddSubview(rarityLabel);

            retailPriceLabel = new UILabel
            {
                Frame = new CGRect(12, 95, ContentView.Bounds.Width - 24, 20),
                Font = UIFont.SystemFontOfSize(13, UIFontWeight.Regular),
                TextColor = UIColor.FromRGB(99, 99, 102)
            };
            ContentView.AddSubview(retailPriceLabel);

            marketPriceLabel = new UILabel
            {
                Frame = new CGRect(12, 120, ContentView.Bounds.Width - 24, 24),
                Font = UIFont.SystemFontOfSize(16, UIFontWeight.Bold),
                TextColor = UIColor.FromRGB(255, 69, 58)
            };
            ContentView.AddSubview(marketPriceLabel);
        }

        public void UpdateCell(PpmtItem item)
        {
            string rarityEmoji = item.Rarity switch
            {
                "Secret" => "💎",
                "Rare" => "⭐",
                _ => "🎁"
            };

            nameLabel!.Text = $"{rarityEmoji} {item.ProductName}";
            rarityLabel!.Text = item.Rarity;
            retailPriceLabel!.Text = $"Retail: ¥{item.RetailPrice:N0}";
            marketPriceLabel!.Text = $"¥{item.AfterMarketPrice:N0}";
        }
    }
}

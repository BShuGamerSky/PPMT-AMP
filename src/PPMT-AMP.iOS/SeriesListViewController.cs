using System;
using System.Collections.Generic;
using System.Linq;
using UIKit;
using CoreGraphics;
using PPMT_AMP.Core.Services;
using PPMT_AMP.Core.Models;

namespace PPMT_AMP.iOS;

public class SeriesListViewController : UIViewController
{
    private UISegmentedControl? viewModeControl;
    private UITableView? seriesTableView;
    private UICollectionView? seriesCollectionView;
    private UIActivityIndicatorView? activityIndicator;
    private UIRefreshControl? refreshControl;
    private UILabel? emptyStateLabel;

    private ApiClient apiClient;
    private string ipCharacter;
    private List<PpmtSeries> allSeries = new();
    private List<PpmtSeries> filteredSeries = new();
    private bool isGridView = false;

    public SeriesListViewController(string ipCharacter)
    {
        this.ipCharacter = ipCharacter;
        apiClient = ApiClient.Instance;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        Title = $"{ipCharacter} Series";
        
        if (View != null)
        {
            View.BackgroundColor = UIColor.FromRGB(245, 245, 250);
            SetupUI();
            LoadSeriesData();
        }
    }

    private void SetupUI()
    {
        if (View == null) return;

        nfloat yOffset = 100;

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
        seriesTableView = new UITableView(new CGRect(0, yOffset, View.Bounds.Width, View.Bounds.Height - yOffset), UITableViewStyle.Plain)
        {
            BackgroundColor = UIColor.FromRGB(245, 245, 250),
            SeparatorStyle = UITableViewCellSeparatorStyle.None,
            ContentInset = new UIEdgeInsets(8, 0, 8, 0)
        };
        seriesTableView.Source = new SeriesTableSource(this);
        
        refreshControl = new UIRefreshControl();
        refreshControl.TintColor = UIColor.FromRGB(255, 69, 58);
        refreshControl.ValueChanged += RefreshControl_ValueChanged;
        seriesTableView.RefreshControl = refreshControl;
        
        View.AddSubview(seriesTableView);

        // Collection View for Grid mode
        var layout = new UICollectionViewFlowLayout
        {
            ScrollDirection = UICollectionViewScrollDirection.Vertical,
            MinimumInteritemSpacing = 12,
            MinimumLineSpacing = 12,
            SectionInset = new UIEdgeInsets(12, 12, 12, 12)
        };
        
        nfloat gridWidth = (View.Bounds.Width - 36) / 2;
        layout.ItemSize = new CGSize(gridWidth, gridWidth * 1.3f);
        
        seriesCollectionView = new UICollectionView(new CGRect(0, yOffset, View.Bounds.Width, View.Bounds.Height - yOffset), layout)
        {
            BackgroundColor = UIColor.FromRGB(245, 245, 250),
            Hidden = true
        };
        seriesCollectionView.RegisterClassForCell(typeof(SeriesGridCell), "SeriesCell");
        seriesCollectionView.Source = new SeriesCollectionSource(this);
        View.AddSubview(seriesCollectionView);

        // Empty state
        emptyStateLabel = new UILabel
        {
            Frame = new CGRect(40, View.Bounds.Height / 2 - 60, View.Bounds.Width - 80, 120),
            Text = "No series found\n\nPull down to refresh",
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

    private async void LoadSeriesData()
    {
        activityIndicator?.StartAnimating();
        emptyStateLabel!.Hidden = true;

        try
        {
            // Query all series for this IP character
            var response = await apiClient.QuerySeriesAsync(ipCharacter);
            
            if (response.Success && response.Data != null)
            {
                allSeries = response.Data;
                filteredSeries = new List<PpmtSeries>(allSeries);
                seriesTableView?.ReloadData();
                seriesCollectionView?.ReloadData();
                
                if (allSeries.Count == 0)
                {
                    emptyStateLabel!.Hidden = false;
                }
            }
            else
            {
                ShowAlert("Load Failed", response.Message ?? "Failed to load series data");
                emptyStateLabel!.Hidden = false;
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Failed to load series: {ex.Message}");
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
        seriesTableView!.Hidden = isGridView;
        seriesCollectionView!.Hidden = !isGridView;
    }

    private async void RefreshControl_ValueChanged(object? sender, EventArgs e)
    {
        try
        {
            var response = await apiClient.QuerySeriesAsync(ipCharacter);
            
            if (response.Success && response.Data != null)
            {
                allSeries = response.Data;
                filteredSeries = new List<PpmtSeries>(allSeries);
                seriesTableView?.ReloadData();
                seriesCollectionView?.ReloadData();
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
    private class SeriesTableSource : UITableViewSource
    {
        private SeriesListViewController parent;
        private const string cellIdentifier = "SeriesCell";

        public SeriesTableSource(SeriesListViewController parent)
        {
            this.parent = parent;
        }

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return parent.filteredSeries.Count;
        }

        public override UITableViewCell GetCell(UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell(cellIdentifier) ?? new UITableViewCell(UITableViewCellStyle.Subtitle, cellIdentifier);
            
            var series = parent.filteredSeries[indexPath.Row];

            cell.BackgroundColor = UIColor.White;
            cell.Layer.CornerRadius = 12;
            cell.Layer.MasksToBounds = true;
            cell.ContentView.BackgroundColor = UIColor.White;

            cell.TextLabel!.Text = $"📦 {series.SeriesName}";
            cell.TextLabel.Font = UIFont.SystemFontOfSize(16, UIFontWeight.Semibold);
            cell.TextLabel.TextColor = UIColor.FromRGB(28, 28, 30);

            string releaseDate = series.ReleaseDate ?? "TBA";
            cell.DetailTextLabel!.Text = $"Released: {releaseDate} • {series.TotalItems} items";
            cell.DetailTextLabel.Font = UIFont.SystemFontOfSize(14, UIFontWeight.Regular);
            cell.DetailTextLabel.TextColor = UIColor.FromRGB(142, 142, 147);

            cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            cell.TintColor = UIColor.FromRGB(255, 69, 58);
            
            return cell;
        }

        public override nfloat GetHeightForRow(UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            return 88;
        }

        public override void RowSelected(UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);
            var series = parent.filteredSeries[indexPath.Row];
            
            var detailVC = new SeriesDetailViewController(series);
            parent.NavigationController?.PushViewController(detailVC, true);
        }
    }

    // Collection Source for Grid View
    private class SeriesCollectionSource : UICollectionViewSource
    {
        private SeriesListViewController parent;

        public SeriesCollectionSource(SeriesListViewController parent)
        {
            this.parent = parent;
        }

        public override nint GetItemsCount(UICollectionView collectionView, nint section)
        {
            return parent.filteredSeries.Count;
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, Foundation.NSIndexPath indexPath)
        {
            var cell = (SeriesGridCell)collectionView.DequeueReusableCell("SeriesCell", indexPath);
            var series = parent.filteredSeries[indexPath.Row];
            cell.UpdateCell(series);
            return cell;
        }

        public override void ItemSelected(UICollectionView collectionView, Foundation.NSIndexPath indexPath)
        {
            var series = parent.filteredSeries[indexPath.Row];
            var detailVC = new SeriesDetailViewController(series);
            parent.NavigationController?.PushViewController(detailVC, true);
        }
    }

    // Grid Cell
    private class SeriesGridCell : UICollectionViewCell
    {
        private UILabel? nameLabel;
        private UILabel? dateLabel;
        private UILabel? itemCountLabel;

        public SeriesGridCell(IntPtr handle) : base(handle)
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
                Frame = new CGRect(12, 12, ContentView.Bounds.Width - 24, 60),
                Font = UIFont.SystemFontOfSize(15, UIFontWeight.Semibold),
                TextColor = UIColor.FromRGB(28, 28, 30),
                Lines = 2
            };
            ContentView.AddSubview(nameLabel);

            dateLabel = new UILabel
            {
                Frame = new CGRect(12, 80, ContentView.Bounds.Width - 24, 20),
                Font = UIFont.SystemFontOfSize(13, UIFontWeight.Regular),
                TextColor = UIColor.FromRGB(142, 142, 147)
            };
            ContentView.AddSubview(dateLabel);

            itemCountLabel = new UILabel
            {
                Frame = new CGRect(12, 105, ContentView.Bounds.Width - 24, 20),
                Font = UIFont.SystemFontOfSize(13, UIFontWeight.Regular),
                TextColor = UIColor.FromRGB(255, 69, 58)
            };
            ContentView.AddSubview(itemCountLabel);
        }

        public void UpdateCell(PpmtSeries series)
        {
            nameLabel!.Text = $"📦 {series.SeriesName}";
            dateLabel!.Text = $"Released: {series.ReleaseDate ?? "TBA"}";
            itemCountLabel!.Text = $"{series.TotalItems} items";
        }
    }
}

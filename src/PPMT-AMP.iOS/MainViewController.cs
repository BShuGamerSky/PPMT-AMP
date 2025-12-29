using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UIKit;
using CoreGraphics;
using PPMT_AMP.Core.Services;
using PPMT_AMP.Core.Models;

namespace PPMT_AMP.iOS;

public class MainViewController : UIViewController
{
    private UILabel? titleLabel;
    private UISearchBar? searchBar;
    private UISegmentedControl? filterControl;
    private UISegmentedControl? viewModeControl;
    private UITableView? itemsTableView;
    private UICollectionView? itemsCollectionView;
    private UIActivityIndicatorView? activityIndicator;
    private UIRefreshControl? refreshControl;
    private UILabel? emptyStateLabel;
    private UIBarButtonItem? profileButton;

    private ApiClient apiClient;
    private AuthService authService;
    private List<PpmtItem> allItems = new();
    private List<PpmtItem> filteredItems = new();
    private bool isGridView = true; // true = grid, false = list

    public MainViewController()
    {
        apiClient = ApiClient.Instance;
        authService = AuthService.Instance;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // Set up the view with PopMart theming
        Title = "PopMart";
        
        // Add profile button to navigation bar with person icon
        if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
        {
            var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Large);
            var personImage = UIImage.GetSystemImage("person.circle.fill", config);
            profileButton = new UIBarButtonItem(personImage, UIBarButtonItemStyle.Plain, ProfileButton_Clicked);
        }
        else
        {
            // Fallback for older iOS
            profileButton = new UIBarButtonItem(UIBarButtonSystemItem.Action, ProfileButton_Clicked);
        }
        profileButton.TintColor = UIColor.FromRGB(255, 69, 58);
        NavigationItem.RightBarButtonItem = profileButton;
        
        if (View != null)
        {
            View.BackgroundColor = UIColor.FromRGB(245, 245, 250);
            SetupUI();
            LoadDataFromBackend();
        }
    }

    private void ProfileButton_Clicked(object? sender, EventArgs e)
    {
        var profileVC = new ProfileViewController();
        NavigationController?.PushViewController(profileVC, true);
    }

    private void SetupUI()
    {
        if (View == null) return;

        nfloat yOffset = 100;
        nfloat margin = 16;

        // Header container
        var headerView = new UIView
        {
            Frame = new CGRect(0, 0, View.Bounds.Width, 280),
            BackgroundColor = UIColor.White
        };
        View.AddSubview(headerView);

        // Title Label
        titleLabel = new UILabel
        {
            Frame = new CGRect(margin, yOffset - 10, View.Bounds.Width - 100, 44),
            Text = "🎁 PopMart",
            Font = UIFont.SystemFontOfSize(34, UIFontWeight.Bold),
            TextColor = UIColor.FromRGB(255, 69, 58),
            TextAlignment = UITextAlignment.Left
        };
        headerView.AddSubview(titleLabel);

        // View Mode Toggle (Grid/List)
        viewModeControl = new UISegmentedControl(new string[] { "Grid", "List" })
        {
            Frame = new CGRect(View.Bounds.Width - 116, yOffset + 8, 100, 28),
            SelectedSegment = 0
        };
        viewModeControl.SelectedSegmentTintColor = UIColor.FromRGB(255, 69, 58);
        var viewModeSelectedAttr = new UIStringAttributes
        {
            ForegroundColor = UIColor.White,
            Font = UIFont.SystemFontOfSize(12, UIFontWeight.Semibold)
        };
        viewModeControl.SetTitleTextAttributes(viewModeSelectedAttr, UIControlState.Selected);
        var viewModeNormalAttr = new UIStringAttributes
        {
            ForegroundColor = UIColor.FromRGB(255, 69, 58),
            Font = UIFont.SystemFontOfSize(12)
        };
        viewModeControl.SetTitleTextAttributes(viewModeNormalAttr, UIControlState.Normal);
        viewModeControl.ValueChanged += ViewModeControl_ValueChanged;
        headerView.AddSubview(viewModeControl);
        
        yOffset += 44;

        // Subtitle
        var subtitleLabel = new UILabel
        {
            Frame = new CGRect(margin, yOffset, View.Bounds.Width - 2 * margin, 20),
            Text = "After-Market Price Tracker",
            Font = UIFont.SystemFontOfSize(15, UIFontWeight.Regular),
            TextColor = UIColor.FromRGB(142, 142, 147),
            TextAlignment = UITextAlignment.Left
        };
        headerView.AddSubview(subtitleLabel);
        yOffset += 28;

        // Search Bar
        searchBar = new UISearchBar
        {
            Frame = new CGRect(margin, yOffset, View.Bounds.Width - 2 * margin, 52),
            Placeholder = "Search products, series, or characters...",
            SearchBarStyle = UISearchBarStyle.Minimal,
            BackgroundColor = UIColor.FromRGB(242, 242, 247)
        };
        searchBar.Layer.CornerRadius = 10;
        searchBar.Layer.MasksToBounds = true;
        searchBar.TextChanged += SearchBar_TextChanged;
        headerView.AddSubview(searchBar);
        yOffset += 62;

        // Filter Control
        filterControl = new UISegmentedControl(new string[] { "All", "Labubu", "Hirono", "Molly", "Skullpanda" })
        {
            Frame = new CGRect(margin, yOffset, View.Bounds.Width - 2 * margin, 32),
            SelectedSegment = 0
        };
        
        if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
        {
            filterControl.SelectedSegmentTintColor = UIColor.FromRGB(255, 69, 58);
        }
        
        var selectedAttributes = new UIStringAttributes
        {
            ForegroundColor = UIColor.White,
            Font = UIFont.SystemFontOfSize(13, UIFontWeight.Semibold)
        };
        filterControl.SetTitleTextAttributes(selectedAttributes, UIControlState.Selected);
        
        var normalAttributes = new UIStringAttributes
        {
            ForegroundColor = UIColor.FromRGB(255, 69, 58),
            Font = UIFont.SystemFontOfSize(13, UIFontWeight.Regular)
        };
        filterControl.SetTitleTextAttributes(normalAttributes, UIControlState.Normal);
        
        filterControl.ValueChanged += FilterControl_ValueChanged;
        headerView.AddSubview(filterControl);

        // Collection View (Grid)
        var layout = new UICollectionViewFlowLayout
        {
            ScrollDirection = UICollectionViewScrollDirection.Vertical,
            MinimumInteritemSpacing = 12,
            MinimumLineSpacing = 12,
            SectionInset = new UIEdgeInsets(12, 12, 12, 12)
        };
        
        nfloat itemWidth = (View.Bounds.Width - 36) / 2; // 2 columns
        layout.ItemSize = new CGSize(itemWidth, itemWidth + 40);
        
        itemsCollectionView = new UICollectionView(new CGRect(0, 280, View.Bounds.Width, View.Bounds.Height - 280), layout)
        {
            BackgroundColor = UIColor.FromRGB(245, 245, 250)
        };
        itemsCollectionView.RegisterClassForCell(typeof(ItemGridCell), "GridCell");
        itemsCollectionView.Source = new ItemsCollectionSource(this);
        
        var collectionRefreshControl = new UIRefreshControl();
        collectionRefreshControl.TintColor = UIColor.FromRGB(255, 69, 58);
        collectionRefreshControl.ValueChanged += RefreshControl_ValueChanged;
        itemsCollectionView.RefreshControl = collectionRefreshControl;
        
        View.AddSubview(itemsCollectionView);

        // Table View (List)
        itemsTableView = new UITableView(new CGRect(0, 280, View.Bounds.Width, View.Bounds.Height - 280), UITableViewStyle.Plain)
        {
            BackgroundColor = UIColor.FromRGB(245, 245, 250),
            SeparatorStyle = UITableViewCellSeparatorStyle.None,
            ContentInset = new UIEdgeInsets(8, 0, 8, 0),
            Hidden = true // Start with grid view
        };
        itemsTableView.Source = new ItemsTableSource(this);
        
        refreshControl = new UIRefreshControl();
        refreshControl.TintColor = UIColor.FromRGB(255, 69, 58);
        refreshControl.ValueChanged += RefreshControl_ValueChanged;
        itemsTableView.RefreshControl = refreshControl;
        
        View.AddSubview(itemsTableView);

        // Empty state label
        emptyStateLabel = new UILabel
        {
            Frame = new CGRect(40, View.Bounds.Height / 2 - 60, View.Bounds.Width - 80, 120),
            Text = "No products found\n\nPull down to refresh and load data",
            Font = UIFont.SystemFontOfSize(17, UIFontWeight.Regular),
            TextColor = UIColor.FromRGB(142, 142, 147),
            TextAlignment = UITextAlignment.Center,
            Lines = 0,
            Hidden = true
        };
        View.AddSubview(emptyStateLabel);

        // Activity Indicator - centered on screen for initial load
        activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
        {
            Center = new CGPoint(View.Center.X, View.Center.Y),
            HidesWhenStopped = true,
            Color = UIColor.FromRGB(255, 69, 58)
        };
        View.AddSubview(activityIndicator);
        
        // Loading splash screen overlay
        var loadingOverlay = new UIView
        {
            Frame = View.Bounds,
            BackgroundColor = UIColor.White,
            Tag = 9999 // Tag for easy removal
        };
        
        var splashLogo = new UILabel
        {
            Frame = new CGRect(40, View.Bounds.Height / 2 - 100, View.Bounds.Width - 80, 120),
            Text = "🎁\nPopMart",
            Font = UIFont.SystemFontOfSize(56, UIFontWeight.Bold),
            TextColor = UIColor.FromRGB(255, 69, 58),
            TextAlignment = UITextAlignment.Center,
            Lines = 2
        };
        loadingOverlay.AddSubview(splashLogo);
        
        var splashSubtitle = new UILabel
        {
            Frame = new CGRect(40, View.Bounds.Height / 2 + 40, View.Bounds.Width - 80, 30),
            Text = "After-Market Price Tracker",
            Font = UIFont.SystemFontOfSize(17, UIFontWeight.Regular),
            TextColor = UIColor.FromRGB(142, 142, 147),
            TextAlignment = UITextAlignment.Center
        };
        loadingOverlay.AddSubview(splashSubtitle);
        
        var splashSpinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
        {
            Center = new CGPoint(View.Center.X, View.Bounds.Height / 2 + 100),
            Color = UIColor.FromRGB(255, 69, 58)
        };
        splashSpinner.StartAnimating();
        loadingOverlay.AddSubview(splashSpinner);
        
        View.AddSubview(loadingOverlay);
    }

    private async void LoadDataFromBackend()
    {
        if (activityIndicator == null) return;
        
        emptyStateLabel!.Hidden = true;

        try
        {
            Console.WriteLine("Loading data from backend API...");
            
            var response = await apiClient.QueryPricesAsync();
            
            if (response.Success && response.Data != null)
            {
                allItems = response.Data;
                filteredItems = new List<PpmtItem>(allItems);
                itemsTableView?.ReloadData();
                itemsCollectionView?.ReloadData();
                
                Console.WriteLine($"Loaded {allItems.Count} items from backend");
                
                if (allItems.Count == 0)
                {
                    emptyStateLabel!.Hidden = false;
                }
            }
            else
            {
                ShowAlert("Load Failed", response.Message ?? "Failed to load data from backend");
                emptyStateLabel!.Hidden = false;
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Failed to load data: {ex.Message}");
            emptyStateLabel!.Hidden = false;
            Console.WriteLine($"Error loading data: {ex.Message}");
        }
        finally
        {
            // Remove splash screen with fade animation
            var splashView = View?.ViewWithTag(9999);
            if (splashView != null)
            {
                UIView.Animate(0.5, () =>
                {
                    splashView.Alpha = 0;
                }, () =>
                {
                    splashView.RemoveFromSuperview();
                });
            }
        }
    }

    private void SearchBar_TextChanged(object? sender, UISearchBarTextChangedEventArgs e)
    {
        FilterItems();
    }

    private void FilterControl_ValueChanged(object? sender, EventArgs e)
    {
        int selectedSegment = (int)(filterControl?.SelectedSegment ?? 0);
        
        if (selectedSegment == 0)
        {
            // "All" tab - stay on current view, show all items
            FilterItems();
        }
        else
        {
            // IP character tab selected - navigate to series list
            string ipCharacter = selectedSegment switch
            {
                1 => "Labubu",
                2 => "Hirono",
                3 => "Molly",
                4 => "Skullpanda",
                _ => "All"
            };

            if (ipCharacter != "All")
            {
                var seriesListVC = new SeriesListViewController(ipCharacter);
                NavigationController?.PushViewController(seriesListVC, true);
                
                // Reset to "All" tab after navigation
                InvokeOnMainThread(() =>
                {
                    filterControl!.SelectedSegment = 0;
                });
            }
        }
    }

    private void FilterItems()
    {
        string searchText = searchBar?.Text?.ToLower() ?? "";
        int selectedFilter = (int)(filterControl?.SelectedSegment ?? 0);

        filteredItems = allItems.Where(item =>
        {
            // Apply search filter
            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                item.ProductName.ToLower().Contains(searchText) ||
                item.IpCharacter.ToLower().Contains(searchText) ||
                item.SeriesName.ToLower().Contains(searchText);

            // When "All" is selected (segment 0), show all items
            // Other segments trigger navigation, so this only handles "All"
            return matchesSearch;
        }).ToList();

        itemsTableView?.ReloadData();
        itemsCollectionView?.ReloadData();
        
        // Show/hide empty state
        if (emptyStateLabel != null)
        {
            emptyStateLabel.Hidden = filteredItems.Count > 0;
        }
    }

    private async void RefreshControl_ValueChanged(object? sender, EventArgs e)
    {
        try
        {
            Console.WriteLine("Refreshing data from backend...");
            
            var response = await apiClient.QueryPricesAsync();
            
            if (response.Success && response.Data != null)
            {
                allItems = response.Data;
                FilterItems();
                
                Console.WriteLine($"Refreshed {allItems.Count} items");
                
                if (allItems.Count == 0)
                {
                    emptyStateLabel!.Hidden = false;
                }
                else
                {
                    emptyStateLabel!.Hidden = true;
                }
            }
            else
            {
                ShowAlert("Refresh Failed", response.Message ?? "Failed to refresh data");
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

    private void ViewModeControl_ValueChanged(object? sender, EventArgs e)
    {
        isGridView = viewModeControl?.SelectedSegment == 0;
        
        if (isGridView)
        {
            itemsCollectionView!.Hidden = false;
            itemsTableView!.Hidden = true;
        }
        else
        {
            itemsCollectionView!.Hidden = true;
            itemsTableView!.Hidden = false;
        }
    }

    // Collection View Source for Grid Layout
    private class ItemsCollectionSource : UICollectionViewSource
    {
        private MainViewController parent;

        public ItemsCollectionSource(MainViewController parent)
        {
            this.parent = parent;
        }

        public override nint GetItemsCount(UICollectionView collectionView, nint section)
        {
            return parent.filteredItems.Count;
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, Foundation.NSIndexPath indexPath)
        {
            var cell = (ItemGridCell)collectionView.DequeueReusableCell("GridCell", indexPath);
            var item = parent.filteredItems[indexPath.Row];
            cell.Configure(item);
            return cell;
        }

        public override void ItemSelected(UICollectionView collectionView, Foundation.NSIndexPath indexPath)
        {
            var item = parent.filteredItems[indexPath.Row];
            decimal priceChange = ((item.AfterMarketPrice - item.RetailPrice) / item.RetailPrice) * 100;
            string details = $"Series: {item.SeriesName}\n" +
                           $"IP Character: {item.IpCharacter}\n" +
                           $"Rarity: {item.Rarity}\n\n" +
                           $"Retail Price: ¥{item.RetailPrice}\n" +
                           $"After-Market Price: ¥{item.AfterMarketPrice}\n" +
                           $"Price Change: +{priceChange:F1}%";
            
            parent.ShowAlert(item.ProductName, details);
        }
    }

    // Grid Cell for Collection View
    private class ItemGridCell : UICollectionViewCell
    {
        private UILabel nameLabel;
        private UILabel rarityLabel;
        private UILabel priceLabel;

        [Export("initWithFrame:")]
        public ItemGridCell(CGRect frame) : base(frame)
        {
            BackgroundColor = UIColor.White;
            Layer.CornerRadius = 12;
            Layer.MasksToBounds = true;

            nameLabel = new UILabel
            {
                Frame = new CGRect(8, frame.Height - 70, frame.Width - 16, 40),
                Font = UIFont.SystemFontOfSize(14, UIFontWeight.Semibold),
                TextColor = UIColor.FromRGB(28, 28, 30),
                Lines = 2,
                LineBreakMode = UILineBreakMode.TailTruncation
            };
            ContentView.AddSubview(nameLabel);

            rarityLabel = new UILabel
            {
                Frame = new CGRect(8, frame.Height - 28, frame.Width - 16, 18),
                Font = UIFont.SystemFontOfSize(11, UIFontWeight.Regular),
                TextColor = UIColor.FromRGB(142, 142, 147)
            };
            ContentView.AddSubview(rarityLabel);

            priceLabel = new UILabel
            {
                Frame = new CGRect(8, 8, frame.Width - 16, 20),
                Font = UIFont.SystemFontOfSize(15, UIFontWeight.Bold),
                TextColor = UIColor.FromRGB(255, 69, 58),
                TextAlignment = UITextAlignment.Right
            };
            ContentView.AddSubview(priceLabel);

            // Placeholder for product image
            var imagePlaceholder = new UIView
            {
                Frame = new CGRect(frame.Width / 4, 35, frame.Width / 2, frame.Width / 2),
                BackgroundColor = UIColor.FromRGB(242, 242, 247),
                Layer = { CornerRadius = 8 }
            };
            ContentView.AddSubview(imagePlaceholder);

            var iconLabel = new UILabel
            {
                Frame = imagePlaceholder.Bounds,
                Text = "🎁",
                Font = UIFont.SystemFontOfSize(40),
                TextAlignment = UITextAlignment.Center
            };
            imagePlaceholder.AddSubview(iconLabel);
        }

        public void Configure(PpmtItem item)
        {
            nameLabel.Text = item.ProductName;
            
            string rarityEmoji = item.Rarity switch
            {
                "Secret" => "⭐",
                "Rare" => "💎",
                _ => "🎯"
            };
            rarityLabel.Text = $"{rarityEmoji} {item.IpCharacter}";
            priceLabel.Text = $"¥{item.AfterMarketPrice}";
        }
    }

    // Custom TableView Source for displaying items
    private class ItemsTableSource : UITableViewSource
    {
        private MainViewController parent;
        private const string cellIdentifier = "ItemCell";

        public ItemsTableSource(MainViewController parent)
        {
            this.parent = parent;
        }

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return parent.filteredItems.Count;
        }

        public override UITableViewCell GetCell(UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell(cellIdentifier) ?? new UITableViewCell(UITableViewCellStyle.Subtitle, cellIdentifier);
            
            var item = parent.filteredItems[indexPath.Row];

            // Modern card-style appearance with spacing
            cell.BackgroundColor = UIColor.White;
            cell.Layer.CornerRadius = 12;
            cell.Layer.MasksToBounds = true;
            cell.ContentView.BackgroundColor = UIColor.White;

            // Configure cell text with modern styling
            cell.TextLabel!.Text = $"🎁 {item.ProductName}";
            cell.TextLabel.Font = UIFont.SystemFontOfSize(16, UIFontWeight.Semibold);
            cell.TextLabel.TextColor = UIColor.FromRGB(28, 28, 30);

            // Calculate price change
            decimal priceChange = ((item.AfterMarketPrice - item.RetailPrice) / item.RetailPrice) * 100;
            string priceChangeSymbol = priceChange > 0 ? "↑" : "↓";

            // Rarity badge emoji
            string rarityEmoji = item.Rarity switch
            {
                "Secret" => "⭐",
                "Rare" => "💎",
                _ => "🎯"
            };

            cell.DetailTextLabel!.Text = $"{rarityEmoji} {item.IpCharacter} • {item.Rarity} | ¥{item.RetailPrice} → ¥{item.AfterMarketPrice} {priceChangeSymbol}{priceChange:F0}%";
            cell.DetailTextLabel.Font = UIFont.SystemFontOfSize(14, UIFontWeight.Regular);
            cell.DetailTextLabel.TextColor = UIColor.FromRGB(142, 142, 147);

            // Modern disclosure indicator
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
            var item = parent.filteredItems[indexPath.Row];
            
            decimal priceChange = ((item.AfterMarketPrice - item.RetailPrice) / item.RetailPrice) * 100;
            string details = $"Series: {item.SeriesName}\n" +
                           $"IP Character: {item.IpCharacter}\n" +
                           $"Rarity: {item.Rarity}\n\n" +
                           $"Retail Price: ¥{item.RetailPrice}\n" +
                           $"After-Market Price: ¥{item.AfterMarketPrice}\n" +
                           $"Price Change: +{priceChange:F1}%";
            
            parent.ShowAlert(item.ProductName, details);
        }
    }
}

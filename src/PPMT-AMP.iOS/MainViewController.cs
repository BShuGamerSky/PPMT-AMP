using System;
using UIKit;
using CoreGraphics;
using PPMT_AMP.Core.Services;
using PPMT_AMP.Core.Models;

namespace PPMT_AMP.iOS;

public class MainViewController : UIViewController
{
    private UILabel? titleLabel;
    private UILabel? statusLabel;
    private UILabel? rateLimitLabel;
    private UIButton? syncButton;
    private UIButton? uploadButton;
    private UIButton? logoutButton;
    private UITableView? dataTableView;
    private UIActivityIndicatorView? activityIndicator;

    private ApiClient apiClient;
    private AuthService authService;
    private List<PriceData> priceDataList = new();

    public MainViewController()
    {
        apiClient = ApiClient.Instance;
        authService = AuthService.Instance;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // Set up the view
        Title = "PPMT-AMP";
        if (View != null)
        {
            View.BackgroundColor = UIColor.SystemBackground;
            SetupUI();
        }
    }

    private void SetupUI()
    {
        if (View == null) return;

        // Title Label
        titleLabel = new UILabel
        {
            Frame = new CGRect(20, 100, View.Bounds.Width - 40, 40),
            Text = "After-Market Price Management",
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.BoldSystemFontOfSize(20),
            TextColor = UIColor.Label
        };
        View.AddSubview(titleLabel);

        // Status Label
        statusLabel = new UILabel
        {
            Frame = new CGRect(20, 145, View.Bounds.Width - 40, 20),
            Text = authService.IsAuthenticated ? "✓ Registered User" : "👤 Visitor Mode (Read-Only)",
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.SystemFontOfSize(14),
            TextColor = authService.IsAuthenticated ? UIColor.SystemGreen : UIColor.SystemOrange
        };
        View.AddSubview(statusLabel);

        // Rate Limit Label
        var (remaining, resetTime) = apiClient.GetRateLimitStatus();
        rateLimitLabel = new UILabel
        {
            Frame = new CGRect(20, 170, View.Bounds.Width - 40, 15),
            Text = $"Requests remaining: {remaining}/20",
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.SystemFontOfSize(12),
            TextColor = UIColor.SystemGray
        };
        View.AddSubview(rateLimitLabel);

        // Sync Button
        syncButton = UIButton.FromType(UIButtonType.System);
        syncButton.Frame = new CGRect(20, 200, View.Bounds.Width - 40, 50);
        syncButton.SetTitle("Query Prices", UIControlState.Normal);
        syncButton.BackgroundColor = UIColor.SystemBlue;
        syncButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        syncButton.Layer.CornerRadius = 8;
        syncButton.TouchUpInside += SyncButton_Clicked;
        View.AddSubview(syncButton);

        // Upload Button
        uploadButton = UIButton.FromType(UIButtonType.System);
        uploadButton.Frame = new CGRect(20, 260, View.Bounds.Width - 40, 50);
        uploadButton.SetTitle("Upload Data", UIControlState.Normal);
        uploadButton.BackgroundColor = authService.IsAuthenticated ? UIColor.SystemGreen : UIColor.SystemGray;
        uploadButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        uploadButton.Layer.CornerRadius = 8;
        uploadButton.Enabled = authService.IsAuthenticated;
        uploadButton.TouchUpInside += UploadButton_Clicked;
        View.AddSubview(uploadButton);

        // Logout Button
        logoutButton = UIButton.FromType(UIButtonType.System);
        logoutButton.Frame = new CGRect(20, 320, View.Bounds.Width - 40, 44);
        logoutButton.SetTitle("Sign Out", UIControlState.Normal);
        logoutButton.SetTitleColor(UIColor.SystemRed, UIControlState.Normal);
        logoutButton.TouchUpInside += LogoutButton_Clicked;
        View.AddSubview(logoutButton);

        // Data Table View
        dataTableView = new UITableView
        {
            Frame = new CGRect(20, 380, View.Bounds.Width - 40, View.Bounds.Height - 440),
            BackgroundColor = UIColor.SystemGray6
        };
        dataTableView.Layer.CornerRadius = 8;
        View.AddSubview(dataTableView);

        // Activity Indicator
        activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
        {
            Center = View.Center,
            HidesWhenStopped = true,
            Color = UIColor.SystemBlue
        };
        View.AddSubview(activityIndicator);
    }

    private async void SyncButton_Clicked(object? sender, EventArgs e)
    {
        if (activityIndicator != null) activityIndicator.StartAnimating();
        if (syncButton != null) syncButton.Enabled = false;

        try
        {
            Console.WriteLine("Querying prices from API...");
            
            var response = await apiClient.QueryPricesAsync();
            
            // Update rate limit display
            if (rateLimitLabel != null && response.RateLimitRemaining.HasValue)
            {
                rateLimitLabel.Text = $"Requests remaining: {response.RateLimitRemaining}/20";
            }

            if (response.Success && response.Data != null)
            {
                priceDataList = response.Data;
                ShowAlert("Success", $"Retrieved {response.Data.Count} price records");
                Console.WriteLine($"Retrieved {response.Data.Count} records");
            }
            else
            {
                ShowAlert("Query Failed", response.Message);
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Failed to query prices: {ex.Message}");
        }
        finally
        {
            if (activityIndicator != null) activityIndicator.StopAnimating();
            if (syncButton != null) syncButton.Enabled = true;
        }
    }

    private async void UploadButton_Clicked(object? sender, EventArgs e)
    {
        if (!authService.IsAuthenticated)
        {
            ShowAlert("Authentication Required", "Please sign in with your AWS credentials to upload data. Visitors have read-only access.");
            return;
        }

        if (activityIndicator != null) activityIndicator.StartAnimating();
        if (uploadButton != null) uploadButton.Enabled = false;

        try
        {
            Console.WriteLine("Uploading data to AWS...");
            
            // Create sample data
            var sampleData = new PriceData
            {
                ProductName = "Test Product",
                MarketPrice = 999.99m,
                RetailPrice = 899.99m
            };
            
            var response = await apiClient.UploadPriceDataAsync(sampleData);
            
            // Update rate limit display
            if (rateLimitLabel != null && response.RateLimitRemaining.HasValue)
            {
                rateLimitLabel.Text = $"Requests remaining: {response.RateLimitRemaining}/20";
            }

            if (response.Success)
            {
                ShowAlert("Success", "Data uploaded successfully!");
            }
            else
            {
                ShowAlert("Upload Failed", response.Message);
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Failed to upload data: {ex.Message}");
        }
        finally
        {
            if (activityIndicator != null) activityIndicator.StopAnimating();
            if (uploadButton != null) uploadButton.Enabled = true;
        }
    }

    private void LogoutButton_Clicked(object? sender, EventArgs e)
    {
        var alert = UIAlertController.Create("Sign Out", "Are you sure you want to sign out?", UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
        alert.AddAction(UIAlertAction.Create("Sign Out", UIAlertActionStyle.Destructive, _ =>
        {
            authService.SignOut();
            NavigationController?.PopViewController(true);
        }));
        PresentViewController(alert, true, null);
    }

    private void ShowAlert(string title, string message)
    {
        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
        PresentViewController(alert, true, null);
    }
}

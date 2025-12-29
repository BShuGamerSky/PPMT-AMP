using System;
using UIKit;
using CoreGraphics;
using PPMT_AMP.Core.Services;

namespace PPMT_AMP.iOS;

public class ProfileViewController : UIViewController
{
    private UIImageView? profileImageView;
    private UILabel? nameLabel;
    private UILabel? statusLabel;
    private UIButton? loginButton;
    private UIButton? logoutButton;
    private UITableView? settingsTableView;

    private AuthService authService;

    public ProfileViewController()
    {
        authService = AuthService.Instance;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        Title = "Profile";
        
        if (View != null)
        {
            View.BackgroundColor = UIColor.FromRGB(245, 245, 250);
            SetupUI();
        }
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        UpdateUI();
    }

    private void SetupUI()
    {
        if (View == null) return;

        nfloat yOffset = 100;
        nfloat margin = 20;

        // Profile container
        var profileContainer = new UIView
        {
            Frame = new CGRect(0, yOffset, View.Bounds.Width, 200),
            BackgroundColor = UIColor.White
        };
        View.AddSubview(profileContainer);

        // Profile image placeholder
        profileImageView = new UIImageView
        {
            Frame = new CGRect((View.Bounds.Width - 80) / 2, 30, 80, 80),
            BackgroundColor = UIColor.FromRGB(255, 69, 58),
            Layer = { CornerRadius = 40, MasksToBounds = true }
        };
        
        // Add person icon using text
        var iconLabel = new UILabel
        {
            Frame = profileImageView.Bounds,
            Text = "👤",
            Font = UIFont.SystemFontOfSize(40),
            TextAlignment = UITextAlignment.Center
        };
        profileImageView.AddSubview(iconLabel);
        profileContainer.AddSubview(profileImageView);

        // Name label
        nameLabel = new UILabel
        {
            Frame = new CGRect(margin, 120, View.Bounds.Width - 2 * margin, 30),
            Text = "Guest User",
            Font = UIFont.SystemFontOfSize(24, UIFontWeight.Bold),
            TextColor = UIColor.FromRGB(28, 28, 30),
            TextAlignment = UITextAlignment.Center
        };
        profileContainer.AddSubview(nameLabel);

        // Status label
        statusLabel = new UILabel
        {
            Frame = new CGRect(margin, 155, View.Bounds.Width - 2 * margin, 20),
            Text = "Not signed in",
            Font = UIFont.SystemFontOfSize(15),
            TextColor = UIColor.FromRGB(142, 142, 147),
            TextAlignment = UITextAlignment.Center
        };
        profileContainer.AddSubview(statusLabel);

        yOffset += 220;

        // Login button (shown when not logged in)
        loginButton = UIButton.FromType(UIButtonType.System);
        loginButton.Frame = new CGRect(margin, yOffset, View.Bounds.Width - 2 * margin, 50);
        loginButton.SetTitle("Sign In", UIControlState.Normal);
        loginButton.BackgroundColor = UIColor.FromRGB(255, 69, 58);
        loginButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        loginButton.Layer.CornerRadius = 12;
        loginButton.TitleLabel!.Font = UIFont.SystemFontOfSize(17, UIFontWeight.Semibold);
        loginButton.TouchUpInside += LoginButton_Clicked;
        View.AddSubview(loginButton);

        // Logout button (shown when logged in)
        logoutButton = UIButton.FromType(UIButtonType.System);
        logoutButton.Frame = new CGRect(margin, yOffset, View.Bounds.Width - 2 * margin, 50);
        logoutButton.SetTitle("Sign Out", UIControlState.Normal);
        logoutButton.BackgroundColor = UIColor.White;
        logoutButton.SetTitleColor(UIColor.FromRGB(255, 69, 58), UIControlState.Normal);
        logoutButton.Layer.CornerRadius = 12;
        logoutButton.Layer.BorderWidth = 2;
        logoutButton.Layer.BorderColor = UIColor.FromRGB(255, 69, 58).CGColor;
        logoutButton.TitleLabel!.Font = UIFont.SystemFontOfSize(17, UIFontWeight.Semibold);
        logoutButton.TouchUpInside += LogoutButton_Clicked;
        logoutButton.Hidden = true;
        View.AddSubview(logoutButton);

        yOffset += 70;

        // Settings table view
        settingsTableView = new UITableView(new CGRect(0, yOffset, View.Bounds.Width, View.Bounds.Height - yOffset), UITableViewStyle.Grouped)
        {
            BackgroundColor = UIColor.FromRGB(245, 245, 250)
        };
        settingsTableView.Source = new SettingsTableSource();
        View.AddSubview(settingsTableView);
    }

    private void UpdateUI()
    {
        bool isAuthenticated = authService.IsAuthenticated;

        if (isAuthenticated)
        {
            nameLabel!.Text = authService.Username ?? "User";
            statusLabel!.Text = "Signed in";
            loginButton!.Hidden = true;
            logoutButton!.Hidden = false;
        }
        else
        {
            nameLabel!.Text = "Guest User";
            statusLabel!.Text = "Not signed in";
            loginButton!.Hidden = false;
            logoutButton!.Hidden = true;
        }
    }

    private void LoginButton_Clicked(object? sender, EventArgs e)
    {
        var loginVC = new LoginViewController();
        var navController = new UINavigationController(loginVC);
        navController.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
        PresentViewController(navController, true, null);
    }

    private void LogoutButton_Clicked(object? sender, EventArgs e)
    {
        var alert = UIAlertController.Create("Sign Out", "Are you sure you want to sign out?", UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
        alert.AddAction(UIAlertAction.Create("Sign Out", UIAlertActionStyle.Destructive, _ =>
        {
            authService.Logout();
            UpdateUI();
        }));
        PresentViewController(alert, true, null);
    }

    private class SettingsTableSource : UITableViewSource
    {
        private string[] items = { "Favorites", "Price Alerts", "Settings", "Help & Support", "About" };

        public override nint RowsInSection(UITableView tableview, nint section)
        {
            return items.Length;
        }

        public override UITableViewCell GetCell(UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell("SettingsCell") ?? new UITableViewCell(UITableViewCellStyle.Default, "SettingsCell");
            cell.TextLabel!.Text = items[indexPath.Row];
            cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            return cell;
        }

        public override void RowSelected(UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);
            // Handle settings navigation
        }
    }
}

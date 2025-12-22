using UIKit;
using CoreGraphics;
using PPMT_AMP.Core.Services;

namespace PPMT_AMP.iOS;

public class LoginViewController : UIViewController
{
    private UILabel? titleLabel;
    private UIButton? loginButton;
    private UIButton? skipButton;
    private UIActivityIndicatorView? activityIndicator;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        Title = "Welcome";
        if (View != null)
        {
            View.BackgroundColor = UIColor.SystemBackground;
            SetupUI();
        }
    }

    private void SetupUI()
    {
        if (View == null) return;

        // Title
        titleLabel = new UILabel
        {
            Frame = new CGRect(20, 100, View.Bounds.Width - 40, 50),
            Text = "PPMT-AMP",
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.BoldSystemFontOfSize(32),
            TextColor = UIColor.Label
        };
        View.AddSubview(titleLabel);

        var subtitleLabel = new UILabel
        {
            Frame = new CGRect(20, 160, View.Bounds.Width - 40, 30),
            Text = "After-Market Price Management",
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.SystemFontOfSize(16),
            TextColor = UIColor.SecondaryLabel
        };
        View.AddSubview(subtitleLabel);

        // Skip Button (Visitor Mode)
        skipButton = UIButton.FromType(UIButtonType.System);
        skipButton.Frame = new CGRect(20, 250, View.Bounds.Width - 40, 50);
        skipButton.SetTitle("Continue as Visitor", UIControlState.Normal);
        skipButton.BackgroundColor = UIColor.SystemGreen;
        skipButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        skipButton.Layer.CornerRadius = 10;
        skipButton.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(18);
        skipButton.TouchUpInside += SkipButton_Clicked;
        View.AddSubview(skipButton);

        // Login Button (Cognito - Future)
        loginButton = UIButton.FromType(UIButtonType.System);
        loginButton.Frame = new CGRect(20, 320, View.Bounds.Width - 40, 50);
        loginButton.SetTitle("Login (Coming Soon)", UIControlState.Normal);
        loginButton.BackgroundColor = UIColor.SystemGray5;
        loginButton.SetTitleColor(UIColor.SystemGray, UIControlState.Normal);
        loginButton.Layer.CornerRadius = 10;
        loginButton.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(18);
        loginButton.Enabled = false; // Disabled until Phase 3
        View.AddSubview(loginButton);

        // Activity Indicator
        activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
        {
            Center = View.Center,
            HidesWhenStopped = true,
            Color = UIColor.SystemBlue
        };
        View.AddSubview(activityIndicator);

        // Info Label
        var infoLabel = new UILabel
        {
            Frame = new CGRect(20, View.Bounds.Height - 180, View.Bounds.Width - 40, 140),
            Text = "👀 Visitor Mode (Default):\n✓ Browse and query prices (read-only)\n✓ View market data\n✗ Cannot modify data\n\n🔐 Login (Phase 3):\n✓ Full access with authentication\n✓ Superusers can manage prices\n\nRate limit: 20 requests per 5 minutes",
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.SystemFontOfSize(12),
            TextColor = UIColor.SystemGray,
            Lines = 0
        };
        View.AddSubview(infoLabel);
    }

    private void SkipButton_Clicked(object? sender, EventArgs e)
    {
        // Set visitor mode and navigate
        AuthService.Instance.SetVisitorMode();
        NavigateToMainScreen();
    }

    private void NavigateToMainScreen()
    {
        var mainViewController = new MainViewController();
        NavigationController?.PushViewController(mainViewController, true);
    }

    private void ShowAlert(string title, string message)
    {
        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
        PresentViewController(alert, true, null);
    }
}

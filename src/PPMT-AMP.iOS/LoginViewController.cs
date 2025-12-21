using UIKit;
using CoreGraphics;
using PPMT_AMP.Core.Services;

namespace PPMT_AMP.iOS;

public class LoginViewController : UIViewController
{
    private UILabel? titleLabel;
    private UITextField? accessKeyField;
    private UITextField? secretKeyField;
    private UIButton? loginButton;
    private UIButton? skipButton;
    private UIActivityIndicatorView? activityIndicator;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        Title = "AWS Login";
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

        // Access Key Field
        var accessKeyLabel = new UILabel
        {
            Frame = new CGRect(20, 220, View.Bounds.Width - 40, 20),
            Text = "AWS Access Key ID",
            Font = UIFont.SystemFontOfSize(14),
            TextColor = UIColor.Label
        };
        View.AddSubview(accessKeyLabel);

        accessKeyField = new UITextField
        {
            Frame = new CGRect(20, 245, View.Bounds.Width - 40, 44),
            Placeholder = "AKIA...",
            BorderStyle = UITextBorderStyle.RoundedRect,
            AutocapitalizationType = UITextAutocapitalizationType.None,
            AutocorrectionType = UITextAutocorrectionType.No,
            KeyboardType = UIKeyboardType.Default
        };
        View.AddSubview(accessKeyField);

        // Secret Key Field
        var secretKeyLabel = new UILabel
        {
            Frame = new CGRect(20, 305, View.Bounds.Width - 40, 20),
            Text = "AWS Secret Access Key",
            Font = UIFont.SystemFontOfSize(14),
            TextColor = UIColor.Label
        };
        View.AddSubview(secretKeyLabel);

        secretKeyField = new UITextField
        {
            Frame = new CGRect(20, 330, View.Bounds.Width - 40, 44),
            Placeholder = "Enter secret key",
            BorderStyle = UITextBorderStyle.RoundedRect,
            SecureTextEntry = true,
            AutocapitalizationType = UITextAutocapitalizationType.None,
            AutocorrectionType = UITextAutocorrectionType.No
        };
        View.AddSubview(secretKeyField);

        // Login Button
        loginButton = UIButton.FromType(UIButtonType.System);
        loginButton.Frame = new CGRect(20, 400, View.Bounds.Width - 40, 50);
        loginButton.SetTitle("Connect to AWS", UIControlState.Normal);
        loginButton.BackgroundColor = UIColor.SystemBlue;
        loginButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        loginButton.Layer.CornerRadius = 10;
        loginButton.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(18);
        loginButton.TouchUpInside += LoginButton_Clicked;
        View.AddSubview(loginButton);

        // Skip Button
        skipButton = UIButton.FromType(UIButtonType.System);
        skipButton.Frame = new CGRect(20, 465, View.Bounds.Width - 40, 44);
        skipButton.SetTitle("Skip (Anonymous Mode)", UIControlState.Normal);
        skipButton.SetTitleColor(UIColor.SystemGray, UIControlState.Normal);
        skipButton.TouchUpInside += SkipButton_Clicked;
        View.AddSubview(skipButton);

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
            Frame = new CGRect(20, View.Bounds.Height - 120, View.Bounds.Width - 40, 80),
            Text = "Visitors: Skip to browse prices (read-only)\nRegistered: Sign in for full access (upload, modify)\n\nRate limit: 20 requests per 5 minutes",
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.SystemFontOfSize(12),
            TextColor = UIColor.SystemGray,
            Lines = 0
        };
        View.AddSubview(infoLabel);
    }

    private async void LoginButton_Clicked(object? sender, EventArgs e)
    {
        var accessKey = accessKeyField?.Text?.Trim() ?? "";
        var secretKey = secretKeyField?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            ShowAlert("Error", "Please enter both Access Key ID and Secret Access Key");
            return;
        }

        if (activityIndicator != null) activityIndicator.StartAnimating();
        if (loginButton != null) loginButton.Enabled = false;
        if (skipButton != null) skipButton.Enabled = false;

        await Task.Run(() =>
        {
            var success = AuthService.Instance.AuthenticateWithAccessKeys(accessKey, secretKey);
            
            InvokeOnMainThread(() =>
            {
                if (activityIndicator != null) activityIndicator.StopAnimating();
                if (loginButton != null) loginButton.Enabled = true;
                if (skipButton != null) skipButton.Enabled = true;

                if (success)
                {
                    NavigateToMainScreen();
                }
                else
                {
                    ShowAlert("Authentication Failed", "Please check your AWS credentials and try again.");
                }
            });
        });
    }

    private void SkipButton_Clicked(object? sender, EventArgs e)
    {
        // Show visitor info
        var alert = UIAlertController.Create(
            "Visitor Mode",
            "As a visitor, you can:\n✓ Browse and query prices\n✓ View market data\n\n✗ Cannot upload data\n✗ Cannot modify prices\n\nTo unlock full features, create an account.",
            UIAlertControllerStyle.Alert
        );
        alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
        alert.AddAction(UIAlertAction.Create("Continue as Visitor", UIAlertActionStyle.Default, _ =>
        {
            AuthService.Instance.AuthenticateAnonymously();
            NavigateToMainScreen();
        }));
        PresentViewController(alert, true, null);
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

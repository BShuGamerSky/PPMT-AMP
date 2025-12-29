using System;
using UIKit;
using CoreGraphics;
using PPMT_AMP.Core.Services;

namespace PPMT_AMP.iOS;

public class LoginViewController : UIViewController
{
    private UILabel? titleLabel;
    private UITextField? usernameField;
    private UITextField? passwordField;
    private UIButton? loginButton;
    private UIButton? skipButton;
    private UIActivityIndicatorView? activityIndicator;

    private AuthService authService;

    public LoginViewController()
    {
        authService = AuthService.Instance;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        Title = "Sign In";
        
        if (View != null)
        {
            View.BackgroundColor = UIColor.FromRGB(245, 245, 250);
            SetupUI();
        }
    }

    private void SetupUI()
    {
        if (View == null) return;

        nfloat centerY = View.Bounds.Height / 2 - 150;
        nfloat margin = 40;

        // Logo/Title
        titleLabel = new UILabel
        {
            Frame = new CGRect(margin, centerY, View.Bounds.Width - 2 * margin, 80),
            Text = "🎁\nPopMart",
            Font = UIFont.SystemFontOfSize(48, UIFontWeight.Bold),
            TextColor = UIColor.FromRGB(255, 69, 58),
            TextAlignment = UITextAlignment.Center,
            Lines = 2
        };
        View.AddSubview(titleLabel);

        // Username field
        usernameField = new UITextField
        {
            Frame = new CGRect(margin, centerY + 100, View.Bounds.Width - 2 * margin, 50),
            Placeholder = "Username",
            BorderStyle = UITextBorderStyle.RoundedRect,
            BackgroundColor = UIColor.White,
            Font = UIFont.SystemFontOfSize(16),
            AutocapitalizationType = UITextAutocapitalizationType.None,
            AutocorrectionType = UITextAutocorrectionType.No
        };
        View.AddSubview(usernameField);

        // Password field
        passwordField = new UITextField
        {
            Frame = new CGRect(margin, centerY + 160, View.Bounds.Width - 2 * margin, 50),
            Placeholder = "Password",
            BorderStyle = UITextBorderStyle.RoundedRect,
            BackgroundColor = UIColor.White,
            Font = UIFont.SystemFontOfSize(16),
            SecureTextEntry = true
        };
        View.AddSubview(passwordField);

        // Login button
        loginButton = UIButton.FromType(UIButtonType.System);
        loginButton.Frame = new CGRect(margin, centerY + 230, View.Bounds.Width - 2 * margin, 50);
        loginButton.SetTitle("Sign In", UIControlState.Normal);
        loginButton.BackgroundColor = UIColor.FromRGB(255, 69, 58);
        loginButton.SetTitleColor(UIColor.White, UIControlState.Normal);
        loginButton.Layer.CornerRadius = 12;
        loginButton.TitleLabel!.Font = UIFont.SystemFontOfSize(17, UIFontWeight.Semibold);
        loginButton.TouchUpInside += LoginButton_Clicked;
        View.AddSubview(loginButton);

        // Skip button
        skipButton = UIButton.FromType(UIButtonType.System);
        skipButton.Frame = new CGRect(margin, centerY + 290, View.Bounds.Width - 2 * margin, 44);
        skipButton.SetTitle("Continue as Guest", UIControlState.Normal);
        skipButton.SetTitleColor(UIColor.FromRGB(142, 142, 147), UIControlState.Normal);
        skipButton.TitleLabel!.Font = UIFont.SystemFontOfSize(15);
        skipButton.TouchUpInside += SkipButton_Clicked;
        View.AddSubview(skipButton);

        // Activity Indicator
        activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium)
        {
            Center = View.Center,
            HidesWhenStopped = true,
            Color = UIColor.FromRGB(255, 69, 58)
        };
        View.AddSubview(activityIndicator);
    }

    private async void LoginButton_Clicked(object? sender, EventArgs e)
    {
        string username = usernameField?.Text ?? "";
        string password = passwordField?.Text ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowAlert("Error", "Please enter both username and password");
            return;
        }

        activityIndicator?.StartAnimating();
        loginButton!.Enabled = false;

        try
        {
            bool success = await authService.LoginWithCognitoAsync(username, password);
            
            if (success)
            {
                DismissViewController(true, null);
            }
            else
            {
                ShowAlert("Login Failed", "Invalid username or password");
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Login failed: {ex.Message}");
        }
        finally
        {
            activityIndicator?.StopAnimating();
            loginButton!.Enabled = true;
        }
    }

    private void SkipButton_Clicked(object? sender, EventArgs e)
    {
        DismissViewController(true, null);
    }

    private void ShowAlert(string title, string message)
    {
        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
        PresentViewController(alert, true, null);
    }
}

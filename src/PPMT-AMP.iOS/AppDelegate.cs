using System;
using Foundation;
using UIKit;

namespace PPMT_AMP.iOS;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // Create the main window
        Window = new UIWindow(UIScreen.MainScreen.Bounds);

        // Start with login screen
        var loginViewController = new LoginViewController();
        var navigationController = new UINavigationController(loginViewController);

        Window.RootViewController = navigationController;
        Window.MakeKeyAndVisible();

        return true;
    }

}

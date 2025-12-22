namespace PPMT_AMP.Core.Services;

/// <summary>
/// Authentication service for user role management
/// </summary>
public class AuthService
{
    private static AuthService? _instance;
    private string _role = "visitor"; // visitor, user, superuser
    private string? _username;
    private string? _userId;

    private AuthService() { }

    public static AuthService Instance
    {
        get
        {
            _instance ??= new AuthService();
            return _instance;
        }
    }

    public string Role => _role;
    public string? Username => _username;
    public string? UserId => _userId;
    public bool IsAuthenticated => _role != "visitor";
    public bool IsSuperuser => _role == "superuser";

    /// <summary>
    /// Set visitor mode (default, no authentication)
    /// </summary>
    public void SetVisitorMode()
    {
        _role = "visitor";
        _username = null;
        _userId = null;
        Console.WriteLine("Using visitor mode (read-only)");
    }

    /// <summary>
    /// Authenticate with Cognito (future implementation)
    /// </summary>
    public Task<bool> LoginWithCognitoAsync(string username, string password)
    {
        // TODO: Implement Cognito authentication in Phase 3
        // For now, return false
        Console.WriteLine("Cognito authentication not yet implemented");
        return Task.FromResult(false);
    }

    /// <summary>
    /// Set superuser role (for testing - will be replaced with Cognito)
    /// </summary>
    public void SetSuperuserRole(string username, string userId)
    {
        _role = "superuser";
        _username = username;
        _userId = userId;
        Console.WriteLine($"Set superuser role: {username}");
    }

    /// <summary>
    /// Logout and return to visitor mode
    /// </summary>
    public void Logout()
    {
        SetVisitorMode();
        Console.WriteLine("Logged out");
    }
}

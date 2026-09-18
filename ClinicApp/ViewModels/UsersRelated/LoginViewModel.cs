using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.UsersRelated;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthenticationService _auth;
    private readonly SessionService _session;
    private readonly RememberMeService _rememberMe;

    // Injects auth, session, and remember-me services.
    public LoginViewModel(AuthenticationService auth, SessionService session, RememberMeService rememberMe)
    {
        _auth = auth;
        _session = session;
        _rememberMe = rememberMe;
    }

    [ObservableProperty] string? identifier;      // username or email
    [ObservableProperty] string? password;
    [ObservableProperty] bool isPasswordHidden = true;
    [ObservableProperty] bool isBusy;

    // Bound to the "Remember me" checkbox on LoginPage.xaml.
    [ObservableProperty] bool rememberMe;

    // Resolves the page currently on screen, to host the popup.
    static Page CurrentPage =>
        Shell.Current?.CurrentPage
        ?? Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("No current page available to host the popup.");

    // Shows an OK-only notice popup — used for validation messages and login failures alike.
    static async Task ShowNoticeAsync(string title, string message)
    {
        var popup = new ConfirmationPopup(title, message, "OK", PopupAction.Positive, showCancelButton: false);
        await CurrentPage.ShowPopupAsync(popup);
    }

    // Flips the password field between hidden and visible.
    [RelayCommand]
    void TogglePasswordVisibility() => IsPasswordHidden = !IsPasswordHidden;

    // Validates input, authenticates, starts the session, and opens the app — the button stays enabled
    // and green regardless of field state; any problem is surfaced via the popup instead of disabling it.
    [RelayCommand]
    async Task Login()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Identifier) || string.IsNullOrWhiteSpace(Password))
        {
            await ShowNoticeAsync("Missing Information", "Please enter both your email/username and password.");
            return;
        }

        if (Password.Length < 6)
        {
            await ShowNoticeAsync("Invalid Password", "Password must be at least 6 characters.");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync(Identifier, Password);

            if (!result.Success || result.User is null)
            {
                await ShowNoticeAsync("Login Failed", result.ErrorMessage ?? "Incorrect email/username or password.");
                Password = null;
                return;
            }

            _session.SignIn(result.User);

            // Only stores anything if the person opted in.
            if (RememberMe)
            {
                await _rememberMe.RememberAsync(result.User.UserID);
            }

            Identifier = null;
            Password = null;
            RememberMe = false;

            NavigationHelper.ShowApp();
        }
        catch (Exception)
        {
            await ShowNoticeAsync("Error", "Something went wrong. Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

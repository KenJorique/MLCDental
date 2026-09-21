using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using ClinicApp.Services.LoginService;
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

        // ── NEW: check once, up front, whether this device even has
        // biometrics enrolled. If not, UseBiometricLock stays false and
        // IsBiometricAvailable stays false — LoginPage.xaml hides the
        // whole toggle via IsVisible="{Binding IsBiometricAvailable}",
        // so a phone with no fingerprint enrolled sees nothing different
        // from before.
        _ = CheckBiometricAvailabilityAsync();
    }

    [ObservableProperty] string? identifier;
    [ObservableProperty] string? password;
    [ObservableProperty] bool isPasswordHidden = true;
    [ObservableProperty] bool isBusy;

    [ObservableProperty] string? errorMessage;

    // Bound to the "Remember me" checkbox on LoginPage.xaml.
    [ObservableProperty] bool rememberMe;

    // Resolves the page currently on screen, to host the popup.
    static Page CurrentPage =>
        Shell.Current?.CurrentPage
        ?? Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("No current page available to host the popup.");
    // ── NEW ──
    [ObservableProperty] bool isBiometricAvailable;
    [ObservableProperty] bool useBiometricLock;

    public bool CanLogin => !IsBusy && !string.IsNullOrWhiteSpace(Identifier) && !string.IsNullOrWhiteSpace(Password);

    // Shows an OK-only notice popup — used for validation messages and login failures alike.
    static async Task ShowNoticeAsync(string title, string message)
    {
        var popup = new ConfirmationPopup(title, message, "OK", PopupAction.Positive, showCancelButton: false);
        await CurrentPage.ShowPopupAsync(popup);
    }

    // Turning off "Remember me" makes the biometric option meaningless
    // (there'd be no token for it to gate) — keep them in sync so the UI
    // can't end up in a confusing half-checked state.
    partial void OnRememberMeChanged(bool value)
    {
        if (!value) UseBiometricLock = false;
    }

    private async Task CheckBiometricAvailabilityAsync()
    {
        IsBiometricAvailable = await _rememberMe.IsBiometricAvailableAsync();
    }

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

            // ── NEW: second factor. Password was correct; now require a
            // fingerprint before the session is created. If the device has
            // no biometrics enrolled, this step is skipped.
            if (IsBiometricAvailable)
            {
                bool confirmed = await _rememberMe.ConfirmBiometricAsync(
                    $"Confirm it's you, {result.User.FullName}");

                if (!confirmed)
                {
                    ErrorMessage = "Fingerprint verification failed or was cancelled.";
                    Password = null;
                    return;
                }
            }

            _session.SignIn(result.User);

            // Only stores anything if the person opted in.
            if (RememberMe)
            {
                await _rememberMe.RememberAsync(result.User.UserID, UseBiometricLock);
            }

            Identifier = null;
            Password = null;
            RememberMe = false;
            UseBiometricLock = false;

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

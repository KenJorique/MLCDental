using ClinicApp.Services;
using ClinicApp.Services.LoginService;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace ClinicApp.ViewModels.UsersRelated;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthenticationService _auth;
    private readonly SessionService _session;
    private readonly RememberMeService _rememberMe;

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

    [ObservableProperty] bool rememberMe;

    // ── NEW ──
    [ObservableProperty] bool isBiometricAvailable;
    [ObservableProperty] bool useBiometricLock;

    public bool CanLogin => !IsBusy && !string.IsNullOrWhiteSpace(Identifier) && !string.IsNullOrWhiteSpace(Password);

    partial void OnIdentifierChanged(string? value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string? value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();

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

    [RelayCommand(CanExecute = nameof(CanLogin))]
    async Task Login()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync(Identifier ?? "", Password ?? "");

            if (!result.Success || result.User is null)
            {
                ErrorMessage = result.ErrorMessage;
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
            ErrorMessage = "Something went wrong. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

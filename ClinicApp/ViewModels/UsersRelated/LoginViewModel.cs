using ClinicApp.Services;
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
    [ObservableProperty] string? errorMessage;

    // Bound to the "Remember me" checkbox on LoginPage.xaml.
    [ObservableProperty] bool rememberMe;

    public bool CanLogin => !IsBusy && !string.IsNullOrWhiteSpace(Identifier) && !string.IsNullOrWhiteSpace(Password);

    // Re-checks CanLogin whenever the identifier changes.
    partial void OnIdentifierChanged(string? value) => LoginCommand.NotifyCanExecuteChanged();
    // Re-checks CanLogin whenever the password changes.
    partial void OnPasswordChanged(string? value) => LoginCommand.NotifyCanExecuteChanged();
    // Re-checks CanLogin whenever the busy state changes.
    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();

    // Flips the password field between hidden and visible.
    [RelayCommand]
    void TogglePasswordVisibility() => IsPasswordHidden = !IsPasswordHidden;

    // Authenticates the user, starts the session, and opens the app.
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
            ErrorMessage = "Something went wrong. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.UsersRelated;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthenticationService _auth;
    private readonly SessionService _session;
    private readonly RememberMeService _rememberMe; // ── NEW ──

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

    // ── NEW: bound to a checkbox on LoginPage.xaml ──
    [ObservableProperty] bool rememberMe;

    public bool CanLogin => !IsBusy && !string.IsNullOrWhiteSpace(Identifier) && !string.IsNullOrWhiteSpace(Password);

    partial void OnIdentifierChanged(string? value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string? value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();

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

            _session.SignIn(result.User);

            // ── NEW: only ever stores anything if the person opted in ──
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

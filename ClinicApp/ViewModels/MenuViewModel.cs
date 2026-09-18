using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.ReportRelated;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.Shared;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.TransactionRelated;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels
{
    public partial class MenuViewModel : ObservableObject
    {
        private readonly SessionService _session;
        private readonly RememberMeService _rememberMe;

        [ObservableProperty] private string googleEmail = "Not connected";
        [ObservableProperty] private string googleButtonText = "Connect";
        [ObservableProperty] private bool isGoogleConnected;
        [ObservableProperty] private string loggedInAs = "";

        // Injects the session and remember-me services.
        public MenuViewModel(SessionService session, RememberMeService rememberMe)
        {
            _session = session;
            _rememberMe = rememberMe;
        }

        // Resolves the page currently on screen, to host the popup.
        static Page CurrentPage =>
            Shell.Current?.CurrentPage
            ?? Application.Current?.Windows.FirstOrDefault()?.Page
            ?? throw new InvalidOperationException("No current page available to host the popup.");

        // Shows the app's Yes/No popup and returns true only if confirmed.
        static async Task<bool> ShowConfirmAsync(string title, string message, string confirmText, PopupAction action)
        {
            var popup = new ConfirmationPopup(title, message, confirmText, action);
            var result = await CurrentPage.ShowPopupAsync(popup);
            return result is true;
        }

        // Refreshes the Google connection status and the logged-in-as line every time the page appears.
        public void OnAppearing()
        {
            try
            {
                var isSignedIn = Preferences.Get("google_signed_in", false);
                IsGoogleConnected = isSignedIn;
                GoogleEmail = isSignedIn
                    ? Preferences.Get("google_email", "Connected")
                    : "Not connected";
                GoogleButtonText = isSignedIn ? "Disconnect" : "Connect";

                LoggedInAs = _session.IsAuthenticated
                    ? $"{_session.FullName} ({_session.Role})"
                    : "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MenuViewModel] OnAppearing error: {ex.Message}");
            }
        }

        // Connects or disconnects the Google account.
        [RelayCommand]
        async Task GoogleSignIn()
        {
            try
            {
                if (Preferences.Get("google_signed_in", false))
                {
                    try { GoogleTasksService.Instance.SignOut(); }
                    catch { /* ignore if not initialized */ }

                    Preferences.Set("google_signed_in", false);
                    Preferences.Set("google_email", "");
                    Preferences.Set("google_access_token", "");

                    IsGoogleConnected = false;
                    GoogleEmail = "Not connected";
                    GoogleButtonText = "Connect";
                }
                else
                {
                    await Shell.Current.GoToAsync(nameof(GoogleSignInPage));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoogleSignIn error: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK");
            }
        }

        // Opens the Services and Pricing page.
        [RelayCommand]
        async Task GoToServices()
        {
            try { await Shell.Current.GoToAsync(nameof(ServiceListPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToServices: {ex.Message}"); }
        }

        // Opens the User Management page.
        [RelayCommand]
        async Task GoToUsers()
        {
            try { await Shell.Current.GoToAsync(nameof(UserListPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToUsers: {ex.Message}"); }
        }

        // Opens the Add Staff page.
        [RelayCommand]
        async Task GoToAddStaff()
        {
            try { await Shell.Current.GoToAsync(nameof(AddUserPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToAddStaff: {ex.Message}"); }
        }

        // Opens the Medical Supply page.
        [RelayCommand]
        async Task GoToSupply()
        {
            try { await Shell.Current.GoToAsync(nameof(SupplyListPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToSupply: {ex.Message}"); }
        }

        // Opens the Balance Management page.
        [RelayCommand]
        async Task GoToPaymentManagement()
        {
            try { await Shell.Current.GoToAsync(nameof(BalanceManagementPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToPaymentManagement: {ex.Message}"); }
        }

        // Opens the Reports page.
        [RelayCommand]
        async Task GoToReports()
        {
            try { await Shell.Current.GoToAsync(nameof(ReportsPage)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MenuViewModel] GoToReports: {ex.Message}"); }
        }

        // Confirms via the app's popup (red, destructive), then signs out and revokes the "remember this device" token — unlike an inactivity timeout, an explicit log out always requires a fresh login next time.
        [RelayCommand]
        async Task Logout()
        {
            bool confirm = await ShowConfirmAsync(
                "Log Out",
                "Are you sure you want to log out?",
                "Log Out",
                PopupAction.Destructive);

            if (!confirm) return;

            int userId = _session.UserId;
            _session.Logout();
            await _rememberMe.ForgetAsync(userId);
        }
    }
}

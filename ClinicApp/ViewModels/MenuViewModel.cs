using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels
{
    public partial class MenuViewModel : ObservableObject
    {
        [ObservableProperty] private string googleEmail = "Not connected";
        [ObservableProperty] private string googleButtonText = "Connect";
        [ObservableProperty] private bool isGoogleConnected;

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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MenuViewModel] OnAppearing error: {ex.Message}");
            }
        }

        [RelayCommand]
        async Task GoogleSignIn()
        {
            try
            {
                if (Preferences.Get("google_signed_in", false))
                {
                    // Sign out safely
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
                System.Diagnostics.Debug.WriteLine(
                    $"[MenuViewModel] GoogleSignIn error: {ex.Message}");
                await Shell.Current.DisplayAlert("Error",
                    $"Navigation failed: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        async Task GoToServices()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(ServiceListPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MenuViewModel] GoToServices: {ex.Message}");
            }
        }

        [RelayCommand]
        async Task GoToUsers()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(UserListPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MenuViewModel] GoToUsers: {ex.Message}");
            }
        }

        [RelayCommand]
        async Task GoToSupply()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(SupplyListPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MenuViewModel] GoToSupply: {ex.Message}");
            }
        }
    }
}
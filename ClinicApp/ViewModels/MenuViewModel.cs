using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels;

public partial class MenuViewModel : ObservableObject
{
    [ObservableProperty] private string googleEmail = "Not connected";
    [ObservableProperty] private string googleButtonText = "Connect";

    // Remove 'override' keyword — ObservableObject doesn't have OnAppearing
    public void OnAppearing()
    {
        var isSignedIn = Preferences.Get("google_signed_in", false);
        GoogleEmail = isSignedIn
            ? Preferences.Get("google_email", "Connected")
            : "Not connected";
        GoogleButtonText = isSignedIn ? "Disconnect" : "Connect";
    }

    [RelayCommand]
    async Task GoogleSignIn()
    {
        if (Preferences.Get("google_signed_in", false))
        {
            // Sign out
            GoogleTasksService.Instance.SignOut();
            Preferences.Set("google_signed_in", false);
            Preferences.Set("google_email", "");
            GoogleEmail = "Not connected";
            GoogleButtonText = "Connect";
        }
        else
        {
            await Shell.Current.GoToAsync(nameof(GoogleSignInPage));
        }
    }

    [RelayCommand]
    async Task GoToServices()
    {
        await Shell.Current.GoToAsync(nameof(ServiceListPage));
    }

    [RelayCommand]
    async Task GoToUsers()
    {
        await Shell.Current.GoToAsync(nameof(UserListPage));
    }

    [RelayCommand]
    async Task GoToSupply()
    {
        await Shell.Current.GoToAsync(nameof(SupplyListPage));
    }
}

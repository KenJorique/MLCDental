using ClinicApp.Services;
using ClinicApp.Services.Database;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;

namespace ClinicApp.Views
{
    public partial class GoogleSignInPage : ContentPage
    {
        public GoogleSignInPage()
        {
            InitializeComponent();
        }

        // Shows a plain OK-only popup, consistent with how alerts are shown elsewhere in the app.
        private async Task ShowAlertAsync(string title, string message)
        {
            var popup = new ConfirmationPopup(title, message, confirmText: "OK", showCancelButton: false);
            await Shell.Current.ShowPopupAsync(popup);
        }

        // Fetches a fresh access token and connects Google Tasks.
        private async void OnSignInClicked(object sender, EventArgs e)
        {
            SignInButton.IsEnabled = false;
            StatusLabel.Text = "Getting access token...";
            StatusLabel.IsVisible = true;

            try
            {
                // Use the refresh token to get a fresh access token automatically
                var supabaseData = Handler?.MauiContext?.Services
                    .GetService<SupabaseDataService>();

                if (supabaseData == null)
                {
                    StatusLabel.IsVisible = false;
                    await ShowAlertAsync("Error", "Service not available.");
                    return;
                }

                var accessToken = await supabaseData.GetFreshAccessTokenAsync();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    await GoogleTasksService.Instance.SignInAsync(accessToken);

                    Preferences.Set("google_signed_in", true);
                    Preferences.Set("google_access_token", accessToken);
                    Preferences.Set("google_email", "mlcdentalclinic1@gmail.com");

                    StatusLabel.IsVisible = false;
                    await ShowAlertAsync("Connected", "Google Tasks is now connected.");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    StatusLabel.IsVisible = false;
                    await ShowAlertAsync("Error", "Failed to get token. Check internet connection.");
                }
            }
            catch (Exception ex)
            {
                StatusLabel.IsVisible = false;
                await ShowAlertAsync("Error", ex.Message);
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleSignIn] {ex.Message}");
            }
            finally
            {
                SignInButton.IsEnabled = true;
            }
        }

        // Records the skip choice and returns without connecting Google Tasks.
        private async void OnSkipClicked(object sender, EventArgs e)
        {
            Preferences.Set("google_signed_in", false);
            await Shell.Current.GoToAsync("..");
        }
    }
}

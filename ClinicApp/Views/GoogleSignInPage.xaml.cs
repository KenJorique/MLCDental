using ClinicApp.Services;

namespace ClinicApp.Views
{
    public partial class GoogleSignInPage : ContentPage
    {
        public GoogleSignInPage()
        {
            InitializeComponent();
        }

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
                    StatusLabel.Text = "Service not available";
                    return;
                }

                var accessToken = await supabaseData.GetFreshAccessTokenAsync();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    await GoogleTasksService.Instance.SignInAsync(accessToken);

                    Preferences.Set("google_signed_in", true);
                    Preferences.Set("google_access_token", accessToken);
                    Preferences.Set("google_email", "ken20042011@gmail.com");

                    StatusLabel.Text = "Connected to Google Tasks!";
                    StatusLabel.TextColor = Colors.Green;

                    await Task.Delay(1500);
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    StatusLabel.Text = "Failed to get token. Check internet connection.";
                    StatusLabel.TextColor = Colors.Red;
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Colors.Red;
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleSignIn] {ex.Message}");
            }
            finally
            {
                SignInButton.IsEnabled = true;
            }
        }

        private async void OnSkipClicked(object sender, EventArgs e)
        {
            Preferences.Set("google_signed_in", false);
            await Shell.Current.GoToAsync("..");
        }
    }
}
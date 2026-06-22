using ClinicApp.Services;

namespace ClinicApp.Views
{
    public partial class GoogleSignInPage : ContentPage
    {
        // Your Google OAuth 2.0 Client ID from Google Cloud Console
        // Go to: APIs & Services → Credentials → your OAuth 2.0 Client ID
        private const string ClientId =
            "697851532160-asig2cht5f61nh5j3gd33baup8c7frcl.apps.googleusercontent.com";

        private const string RedirectUri =
            "com.mlcdental.clinicapp:/oauth2redirect";

        public GoogleSignInPage()
        {
            InitializeComponent();
        }

        private async void OnSignInClicked(object sender, EventArgs e)
        {
            SignInButton.IsEnabled = false;
            StatusLabel.Text = "Opening Google sign-in...";
            StatusLabel.IsVisible = true;

            try
            {
                // Build Google OAuth URL
                var authUrl = new Uri(
                    "https://accounts.google.com/o/oauth2/v2/auth" +
                    $"?client_id={ClientId}" +
                    $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                    "&response_type=code" +
                    "&scope=" + Uri.EscapeDataString(
                        "https://www.googleapis.com/auth/tasks " +
                        "email profile") +
                    "&access_type=offline" +
                    "&prompt=consent");

                var callbackUri = new Uri(RedirectUri);

                // MAUI built-in WebAuthenticator — no extra packages needed
                var result = await WebAuthenticator.Default
                    .AuthenticateAsync(authUrl, callbackUri);

                var authCode = result.Properties.GetValueOrDefault("code");

                if (!string.IsNullOrEmpty(authCode))
                {
                    StatusLabel.Text = "Exchanging token...";

                    // Exchange auth code for access token
                    var tokens = await ExchangeCodeForTokenAsync(authCode);

                    if (tokens != null)
                    {
                        await GoogleTasksService.Instance
                            .SignInAsync(tokens.AccessToken);

                        Preferences.Set("google_signed_in", true);
                        Preferences.Set("google_email",
                            tokens.Email ?? "Connected");
                        Preferences.Set("google_refresh_token",
                            tokens.RefreshToken ?? "");
                        // After getting tokens, add:
                        Preferences.Set("google_access_token", tokens.AccessToken ?? "");

                        StatusLabel.Text = $"Connected as {tokens.Email}";
                        StatusLabel.TextColor = Colors.Green;

                        await Task.Delay(1000);
                        await Shell.Current.GoToAsync("..");
                    }
                    else
                    {
                        StatusLabel.Text = "Token exchange failed";
                        StatusLabel.TextColor = Colors.Red;
                    }
                }
                else
                {
                    StatusLabel.Text = "Sign-in cancelled or failed";
                }
            }
            catch (TaskCanceledException)
            {
                StatusLabel.Text = "Sign-in cancelled";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleSignIn] {ex.Message}");
            }
            finally
            {
                SignInButton.IsEnabled = true;
            }
        }

        private async Task<GoogleTokens?> ExchangeCodeForTokenAsync(string code)
        {
            try
            {
                using var http = new HttpClient();
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = ClientId,
                    ["redirect_uri"] = RedirectUri,
                    ["grant_type"] = "authorization_code"
                });

                // Call Supabase Edge Function to exchange — keeps client_secret safe
                var json = await new HttpClient().PostAsync(
                    "https://uxacdqkkocbjaiqszpyk.supabase.co/functions/v1/exchange-google-token",
                    new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(
                            new { code, redirect_uri = RedirectUri }),
                        System.Text.Encoding.UTF8, "application/json"));

                var content = await json.Content.ReadAsStringAsync();
                var doc = System.Text.Json.JsonDocument.Parse(content);

                return new GoogleTokens
                {
                    AccessToken = doc.RootElement
                                      .GetProperty("access_token").GetString(),
                    RefreshToken = doc.RootElement.TryGetProperty(
                                      "refresh_token", out var rt)
                                      ? rt.GetString() : null,
                    Email = doc.RootElement.TryGetProperty(
                                      "email", out var em)
                                      ? em.GetString() : null
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ExchangeToken] {ex.Message}");
                return null;
            }
        }

        private async void OnSkipClicked(object sender, EventArgs e)
        {
            Preferences.Set("google_signed_in", false);
            await Shell.Current.GoToAsync("..");
        }
    }

    public class GoogleTokens
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Email { get; set; }
    }
}
using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels
{
    public partial class ProfileViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly SupabaseDataService _supabaseData;
        private readonly SessionService _session;
        private readonly RememberMeService _rememberMe;

        // The signed-in staff member's full local record, loaded once and reused for edits.
        private User? _user;

        [ObservableProperty] private string fullName = "";
        [ObservableProperty] private string role = "";

        [ObservableProperty] private string contactNo = "";
        [ObservableProperty] private string email = "";
        [ObservableProperty] private bool isEditingContact;
        [ObservableProperty] private bool isSaving;

        [ObservableProperty] private string googleEmail = "Not connected";
        [ObservableProperty] private string googleButtonText = "Connect";
        [ObservableProperty] private bool isGoogleConnected;

        // Injects the local/remote data services, session, and remember-me service.
        public ProfileViewModel(DatabaseService db, SupabaseDataService supabaseData,
            SessionService session, RememberMeService rememberMe)
        {
            _db = db;
            _supabaseData = supabaseData;
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

        // Shows an OK-only notice popup.
        static async Task ShowNoticeAsync(string title, string message)
        {
            var popup = new ConfirmationPopup(title, message, "OK", PopupAction.Positive, showCancelButton: false);
            await CurrentPage.ShowPopupAsync(popup);
        }

        // Loads the signed-in user's full record and the current Google connection status.
        [RelayCommand]
        public async Task LoadAsync()
        {
            try
            {
                _user = (await _db.GetUsers()).FirstOrDefault(u => u.UserID == _session.UserId);
                if (_user is null) return;

                FullName = _user.FullName ?? "";
                Role = _user.Role ?? "";
                ContactNo = _user.ContactNo ?? "";
                Email = _user.Email ?? "";

                var isSignedIn = Preferences.Get("google_signed_in", false);
                IsGoogleConnected = isSignedIn;
                GoogleEmail = isSignedIn ? Preferences.Get("google_email", "Connected") : "Not connected";
                GoogleButtonText = isSignedIn ? "Disconnect" : "Connect";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileViewModel] LoadAsync error: {ex.Message}");
            }
        }

        // Switches the Contact Info card into edit mode.
        [RelayCommand]
        void EditContact() => IsEditingContact = true;

        // Confirms via the red popup, then discards edits and reverts the fields to the last loaded values.
        [RelayCommand]
        async Task CancelEditContact()
        {
            bool confirm = await ShowConfirmAsync(
                "Discard Changes?",
                "Discard your changes to contact info?",
                "Discard", PopupAction.Destructive);
            if (!confirm) return;

            ContactNo = _user?.ContactNo ?? "";
            Email = _user?.Email ?? "";
            IsEditingContact = false;
        }

        // Confirms, then saves Contact Number/Email locally and to Supabase — Full Name and Role stay admin-only and are never touched here.
        [RelayCommand]
        async Task SaveContact()
        {
            if (_user is null || IsSaving) return;

            bool confirm = await ShowConfirmAsync(
                "Save Changes",
                "Save your updated contact information?",
                "Save", PopupAction.Positive);
            if (!confirm) return;

            IsSaving = true;
            try
            {
                _user.ContactNo = ContactNo.Trim();
                _user.Email = Email.Trim();

                await _db.UpdateUser(_user);

                if (!string.IsNullOrEmpty(_user.SupabaseId))
                {
                    var remote = new SupabaseUser
                    {
                        Id = _user.SupabaseId,
                        FullName = _user.FullName,
                        Username = _user.Username,
                        PasswordHash = _user.PasswordHash,
                        Role = _user.Role,
                        ContactNo = _user.ContactNo,
                        Email = _user.Email,
                        IsActive = _user.IsActive,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    await _supabaseData.UpdateUserAsync(remote);
                }

                IsEditingContact = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileViewModel] SaveContact error: {ex.Message}");
                await ShowNoticeAsync("Error", $"Couldn't save your changes: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        // Connects or disconnects the Google account — moved here from MenuViewModel unchanged.
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
                System.Diagnostics.Debug.WriteLine($"[ProfileViewModel] GoogleSignIn error: {ex.Message}");
                await ShowNoticeAsync("Error", $"Navigation failed: {ex.Message}");
            }
        }

        // Confirms via the red popup, then signs out and revokes the "remember this device" token.
        [RelayCommand]
        async Task Logout()
        {
            bool confirm = await ShowConfirmAsync(
                "Log Out",
                "Are you sure you want to log out?",
                "Log Out", PopupAction.Destructive);
            if (!confirm) return;

            int userId = _session.UserId;
            _session.Logout();
            await _rememberMe.ForgetAsync(userId);
        }
    }
}

using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.UsersRelated;

[QueryProperty(nameof(UserId), "UserId")]
public partial class AddUserViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly SupabaseDataService _supabaseData;

    // Tracks whether this user already has a Supabase row (update) or not (insert).
    private string _existingSupabaseId = "";

    // Captured on load so SaveUser can detect an Active → Inactive edit.
    private bool _wasActiveBeforeEdit = true;

    // Injects the local and Supabase data services.
    public AddUserViewModel(DatabaseService db, SupabaseDataService supabaseData)
    {
        _db = db;
        _supabaseData = supabaseData;
    }

    // ---------------------------------------------------------------
    // ConfirmationPopup helpers — replace Shell.Current.DisplayAlert
    // everywhere in this ViewModel with the app's dimmed-backdrop
    // rounded-card popup.
    // ---------------------------------------------------------------

    // Resolves the page currently on screen, to host the popup.
    static Page CurrentPage =>
        Shell.Current?.CurrentPage
        ?? Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("No current page available to host the popup.");

    // Shows a Yes/No popup and returns true only if confirmed.
    static async Task<bool> ShowConfirmAsync(
        string title, string message, string confirmText = "Yes", Color? confirmColor = null)
    {
        var popup = new ConfirmationPopup(title, message, confirmText, confirmColor);
        var result = await CurrentPage.ShowPopupAsync(popup);
        return result is true;
    }

    // Shows a plain OK-only notice popup.
    static async Task ShowNoticeAsync(string title, string message, string okText = "OK")
    {
        var popup = new ConfirmationPopup(title, message, okText, null, showCancelButton: false);
        await CurrentPage.ShowPopupAsync(popup);
    }

    // Shows an OK-only error popup titled "Error".
    static Task ShowErrorAsync(string message) => ShowNoticeAsync("Error", message);

    // ─── Fields ─────────────────────────────────────────────
    [ObservableProperty] int userId;
    [ObservableProperty] string pageTitle = "Add Staff";
    [ObservableProperty] string? fullName;
    [ObservableProperty] string? username;
    [ObservableProperty] string? password;
    [ObservableProperty] string? role;
    [ObservableProperty] string? contactNo;
    [ObservableProperty] string? email;

    // Newly added users are Active by default
    [ObservableProperty] bool isActive = true;

    // Controls whether the Active/Inactive switch is shown (only on edit)
    [ObservableProperty] bool isEditMode = false;

    // Switches the page into edit mode and loads the user once UserId arrives.
    partial void OnUserIdChanged(int value)
    {
        if (value > 0)
        {
            PageTitle = "Edit Staff";
            IsEditMode = true; // show the status switch only on edit
            LoadUserData(value);
        }
    }

    // Loads an existing user's fields into the form for editing.
    private async void LoadUserData(int id)
    {
        var user = (await _db.GetUsers()).FirstOrDefault(u => u.UserID == id);
        if (user != null)
        {
            FullName = user.FullName;
            Username = user.Username;
            // Password left blank on purpose — blank keeps the current one.
            Role = user.Role;
            ContactNo = user.ContactNo;
            Email = user.Email;
            IsActive = user.IsActive;
            _wasActiveBeforeEdit = user.IsActive;
            _existingSupabaseId = user.SupabaseId;
        }
    }

    // Validates, saves the user locally and to Supabase, then goes back.
    [RelayCommand]
    async Task SaveUser()
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Role))
        {
            await ShowNoticeAsync("Validation", "Full name and role are required.");
            return;
        }

        // A brand-new account must be given a password here (edit mode can
        // leave it blank to keep the current one — see AddUser/UpdateUser).
        if (UserId == 0 && string.IsNullOrWhiteSpace(Password))
        {
            await ShowNoticeAsync("Validation", "A password is required for a new account.");
            return;
        }

        var user = new User
        {
            UserID = UserId,
            FullName = FullName,
            Username = Username,
            Password = Password,
            Role = Role,
            ContactNo = ContactNo,
            Email = Email,
            // New users default to Active; edit mode uses the switch value
            IsActive = UserId > 0 ? IsActive : true
        };

        var isNewUser = UserId == 0;

        if (UserId > 0)
            await _db.UpdateUser(user); // hashes Password if provided, preserves everything else
        else
            await _db.AddUser(user);    // sets user.UserID + user.PasswordHash on the object

        await SyncUserToSupabaseAsync(user);

        if (isNewUser)
            await _supabaseData.LogActivityAsync("NewUser", $"New staff account {user.FullName} added");
        else if (_wasActiveBeforeEdit && !IsActive)
            await _supabaseData.LogActivityAsync("UserDeactivated", $"{user.FullName}'s account was deactivated");

        await Shell.Current.GoToAsync("..");
    }

    // Discards and goes back — called by the page's back-button override too.
    [RelayCommand]
    async Task Cancel() => await Shell.Current.GoToAsync("..");

    // Mirrors the saved user to Supabase so the account works from other devices too.
    private async Task SyncUserToSupabaseAsync(User user)
    {
        try
        {
            var remote = new SupabaseUser
            {
                Id = _existingSupabaseId,
                FullName = user.FullName,
                Username = user.Username,
                PasswordHash = user.PasswordHash, // hash only — see SupabaseUser.cs note
                Role = user.Role,
                ContactNo = user.ContactNo,
                Email = user.Email,
                IsActive = user.IsActive,
                UpdatedAt = DateTime.UtcNow,
            };

            if (string.IsNullOrEmpty(_existingSupabaseId))
            {
                var saved = await _supabaseData.AddUserAsync(remote);

                // ── TEMP DIAGNOSTIC — remove once this is confirmed working ──
                if (saved is null || string.IsNullOrEmpty(saved.Id))
                {
                    await ShowNoticeAsync(
                        "Supabase sync",
                        "Insert returned no row — check that users_table.sql (and its RLS policy) has been run.");
                    return;
                }
                // ── end temp diagnostic ──

                await _db.SetUserSupabaseId(user.UserID, saved.Id);
            }
            else
            {
                var updated = await _supabaseData.UpdateUserAsync(remote);

                // ── TEMP DIAGNOSTIC — remove once this is confirmed working ──
                if (!updated)
                {
                    await ShowNoticeAsync(
                        "Supabase sync",
                        "Update failed — check the Debug Output window for the [Supabase] UpdateUser FAILED line.");
                }
                // ── end temp diagnostic ──
            }
        }
        catch (Exception ex)
        {
            // ── TEMP DIAGNOSTIC — remove once this is confirmed working ──
            await ShowErrorAsync(ex.Message);
            // ── end temp diagnostic ──

            System.Diagnostics.Debug.WriteLine($"[SyncUserToSupabase] {ex.Message}");
        }
    }
}

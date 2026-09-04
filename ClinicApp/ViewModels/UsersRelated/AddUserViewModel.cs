using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.UsersRelated;

[QueryProperty(nameof(UserId), "UserId")]
public partial class AddUserViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly SupabaseDataService _supabaseData;

    // Not a form field — just carried along so SaveUser knows whether this
    // user already has a Supabase row (update) or not (insert).
    private string _existingSupabaseId = "";

    // Injects the local and Supabase data services.
    public AddUserViewModel(DatabaseService db, SupabaseDataService supabaseData)
    {
        _db = db;
        _supabaseData = supabaseData;
    }

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

    // Loads the user for editing when UserId is set via navigation.
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
            // Password intentionally left blank — leave blank to keep the current one, or type a new one to change it.
            Role = user.Role;
            ContactNo = user.ContactNo;
            Email = user.Email;
            IsActive = user.IsActive;
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
            await Shell.Current.DisplayAlert("Validation", "Full name and role are required.", "OK");
            return;
        }

        // A brand-new account must be given a password here (edit mode can
        // leave it blank to keep the current one — see AddUser/UpdateUser).
        if (UserId == 0 && string.IsNullOrWhiteSpace(Password))
        {
            await Shell.Current.DisplayAlert("Validation", "A password is required for a new account.", "OK");
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

        if (UserId > 0)
            await _db.UpdateUser(user); // hashes Password if provided, preserves everything else
        else
            await _db.AddUser(user);    // sets user.UserID + user.PasswordHash on the object

        await SyncUserToSupabaseAsync(user);

        await Shell.Current.GoToAsync("..");
    }

    // Discards and goes back — this is what the page's back-button override calls.
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
                    await Shell.Current.DisplayAlert(
                        "Supabase sync",
                        "Insert returned no row. Either the 'users' table " +
                        "doesn't exist yet, or Row Level Security is blocking " +
                        "the anon key. Check that users_table.sql was run, " +
                        "including its RLS policy.",
                        "OK");
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
                    await Shell.Current.DisplayAlert(
                        "Supabase sync",
                        "Update failed — check the Debug Output window for " +
                        "the [Supabase] UpdateUser FAILED line.",
                        "OK");
                }
                // ── end temp diagnostic ──
            }
        }
        catch (Exception ex)
        {
            // ── TEMP DIAGNOSTIC — remove once this is confirmed working ──
            await Shell.Current.DisplayAlert("Supabase sync error", ex.Message, "OK");
            // ── end temp diagnostic ──

            System.Diagnostics.Debug.WriteLine($"[SyncUserToSupabase] {ex.Message}");
        }
    }
}

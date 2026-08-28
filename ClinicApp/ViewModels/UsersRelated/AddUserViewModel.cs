using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.UsersRelated;

[QueryProperty(nameof(UserId), "UserId")]
public partial class AddUserViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    // Injects the database service used for reading/writing staff records.
    public AddUserViewModel(DatabaseService db)
    {
        _db = db;
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

    // Tracks whether the user has made any unsaved edits, so Cancel / the
    // back arrow know whether a "discard changes?" prompt is actually needed.
    bool _isLoading;
    bool _isDirty;

    // Flags the form as dirty when the name field changes.
    partial void OnFullNameChanged(string? value) => MarkDirty();
    // Flags the form as dirty when the username field changes.
    partial void OnUsernameChanged(string? value) => MarkDirty();
    // Flags the form as dirty when the password field changes.
    partial void OnPasswordChanged(string? value) => MarkDirty();
    // Flags the form as dirty when the role changes.
    partial void OnRoleChanged(string? value) => MarkDirty();
    // Flags the form as dirty when the contact number changes.
    partial void OnContactNoChanged(string? value) => MarkDirty();
    // Flags the form as dirty when the email changes.
    partial void OnEmailChanged(string? value) => MarkDirty();
    // Flags the form as dirty when the active/inactive switch changes.
    partial void OnIsActiveChanged(bool value) => MarkDirty();

    // Marks the form dirty, unless we're still loading initial data.
    void MarkDirty()
    {
        if (!_isLoading)
            _isDirty = true;
    }

    // Automatically called when UserId is set via navigation query param
    partial void OnUserIdChanged(int value)
    {
        if (value > 0)
        {
            PageTitle = "Edit Staff";
            IsEditMode = true; // show the status switch only on edit
            _ = LoadUserData(value);
        }
    }

    // Loads an existing staff member's data into the form fields for editing.
    private async Task LoadUserData(int id)
    {
        var user = (await _db.GetUsers()).FirstOrDefault(u => u.UserID == id);
        if (user != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _isLoading = true;
                FullName = user.FullName;
                Username = user.Username;
                Password = user.Password;
                Role = user.Role;
                ContactNo = user.ContactNo;
                Email = user.Email;
                IsActive = user.IsActive;
                _isLoading = false;
                _isDirty = false; // freshly loaded data isn't a user edit
            });
        }
    }

    // ─── Save command ────────────────────────────────────────
    // Validates the form, confirms with the user, then saves to the database.

    [RelayCommand]
    async Task SaveUser()
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Role))
        {
            await Shell.Current.DisplayAlert("Validation", "Full name and role are required.", "OK");
            return;
        }

        bool confirmSave = await Shell.Current.DisplayAlert(
            "Confirm Save",
            UserId > 0
                ? "Are you sure you want to save these changes?"
                : "Are you sure you want to add this staff record?",
            "Save", "Cancel");

        if (!confirmSave)
            return;

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
            await _db.UpdateUser(user);
        else
            await _db.AddUser(user);

        _isDirty = false;

        await Shell.Current.DisplayAlert(
            "Saved",
            UserId > 0
                ? "The staff member has been updated successfully."
                : "The staff member has been saved successfully.",
            "OK");

        await Shell.Current.GoToAsync("..");
    }

    // ─── Cancel command ──────────────────────────────────────

    [RelayCommand]
    async Task Cancel()
    {
        if (_isDirty)
        {
            bool discard = await Shell.Current.DisplayAlert(
                "Discard changes?",
                "Are you sure you want to discard the changes you made?",
                "Discard", "Keep Editing");

            if (!discard)
                return;
        }

        await Shell.Current.GoToAsync("..");
    }
}

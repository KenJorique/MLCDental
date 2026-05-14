using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels.UsersRelated;

[QueryProperty(nameof(UserId), "UserId")]
public partial class AddUserViewModel : ObservableObject
{
    private readonly DatabaseService _db;

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

    // Automatically called when UserId is set via navigation query param
    partial void OnUserIdChanged(int value)
    {
        if (value > 0)
        {
            PageTitle = "Edit Staff";
            IsEditMode = true; // show the status switch only on edit
            LoadUserData(value);
        }
    }

    // Loads existing user data into the form fields
    private async void LoadUserData(int id)
    {
        var user = (await _db.GetUsers()).FirstOrDefault(u => u.UserID == id);
        if (user != null)
        {
            FullName = user.FullName;
            Username = user.Username;
            Password = user.Password;
            Role = user.Role;
            ContactNo = user.ContactNo;
            Email = user.Email;
            IsActive = user.IsActive;
        }
    }

    // Saves the user (add or update) then navigates back
    [RelayCommand]
    async Task SaveUser()
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Role))
        {
            await Shell.Current.DisplayAlert("Validation", "Full name and role are required.", "OK");
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
            await _db.UpdateUser(user);
        else
            await _db.AddUser(user);

        await Shell.Current.GoToAsync("..");
    }
}

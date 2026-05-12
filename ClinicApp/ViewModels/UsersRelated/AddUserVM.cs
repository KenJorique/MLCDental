using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Data;

namespace ClinicApp.ViewModels.UsersRelated;

[QueryProperty(nameof(UserId), "UserId")]
public partial class AddUserViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    public AddUserViewModel(DatabaseService db)
    {
        _db = db;
    }

    [ObservableProperty] int userId;
    [ObservableProperty] string fullName;
    [ObservableProperty] string username;
    [ObservableProperty] string password;
    [ObservableProperty] string role;

    partial void OnUserIdChanged(int value)
    {
        if (value > 0) LoadUserData(value);
    }

    private async void LoadUserData(int id)
    {
        var user = (await _db.GetUsers()).FirstOrDefault(u => u.UserID == id);
        if (user != null)
        {
            FullName = user.FullName;
            Username = user.Username;
            Password = user.Password;
            Role = user.Role;
        }
    }

    [RelayCommand]
    async Task SaveUser()
    {
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Role)) return;

        var user = new User
        {
            UserID = UserId,
            FullName = FullName,
            Username = Username,
            Password = Password,
            Role = Role
        };

        if (UserId > 0)
            await _db.UpdateUser(user); // Ensure UpdateUser exists in DatabaseService
        else
            await _db.AddUser(user); // Ensure AddUser exists in DatabaseService

        await Shell.Current.GoToAsync("..");
    }
}
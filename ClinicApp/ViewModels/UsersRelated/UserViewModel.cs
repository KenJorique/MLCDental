using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels;

public partial class UserViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    public ObservableCollection<User> Users { get; set; } = new();
    

    public UserViewModel(DatabaseService db)
    {
        _db = db;
    }

    [RelayCommand]
    public async Task LoadUsers()
    {
        Users.Clear();
        var list = await _db.GetUsers(); // Ensure this method exists in DatabaseService
        foreach (var user in list)
            Users.Add(user);
    }

    [RelayCommand]
    public async Task DeleteUser(User user)
    {
        if (user == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Confirm", $"Delete {user.FullName}?", "Yes", "No");
        if (confirm)
        {
            await _db.DeleteUser(user);
            await LoadUsers();
        }
    }

    [RelayCommand]
    async Task GoToAddUser()
    {
        await Shell.Current.GoToAsync(nameof(AddUserPage));
    }

    [RelayCommand]
    async Task EditUser(User user)
    {
        if (user == null) return;
        await Shell.Current.GoToAsync($"{nameof(AddUserPage)}?UserId={user.UserID}");
    }
}
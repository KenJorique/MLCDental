using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.UsersRelated;

public partial class UserViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    // Wrapped user cards shown in the list
    public ObservableCollection<UserCardViewModel> Users { get; set; } = new();

    public UserViewModel(DatabaseService db)
    {
        _db = db;
    }

    // Loads all users from the database and wraps them in UserCardViewModel
    [RelayCommand]
    public async Task LoadUsers()
    {
        Users.Clear();
        var list = await _db.GetUsers();
        foreach (var user in list)
            Users.Add(new UserCardViewModel(user));
    }

    // Navigates to AddUserPage pre-filled with the selected user's data
    [RelayCommand]
    public async Task EditUser(UserCardViewModel card)
    {
        if (card == null) return;
        await Shell.Current.GoToAsync($"{nameof(AddUserPage)}?UserId={card.User.UserID}");
    }

    // Deletes a user after confirmation, then reloads the list
    [RelayCommand]
    public async Task DeleteUser(UserCardViewModel card)
    {
        if (card == null) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Confirm Delete",
            $"Are you sure you want to delete {card.User.FullName}?",
            "Yes", "No");

        if (confirm)
        {
            await _db.DeleteUser(card.User);
            await LoadUsers();
        }
    }

    // Navigates to AddUserPage for adding a new user
    [RelayCommand]
    async Task GoToAddUser()
    {
        await Shell.Current.GoToAsync(nameof(AddUserPage));
    }
}

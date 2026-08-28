using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace ClinicApp.ViewModels.UsersRelated;

public partial class UserViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;

    // The staff list bound to the CollectionView.
    public ObservableCollection<UserCardViewModel> Users { get; set; } = new();

    // Injects the database service used for reading/writing staff records.
    public UserViewModel(DatabaseService db) => _db = db;

    // Loads (or reloads) the staff list from the database.
    [RelayCommand]
    public async Task LoadUsers()
    {
        if (IsBusy) return; // Note: Toolkit generates uppercase 'IsBusy'
        IsBusy = true;
        try
        {
            var list = await _db.GetUsers();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Users.Clear();
                foreach (var user in list)
                    Users.Add(new UserCardViewModel(user));
            });
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    // Opens the Edit/Delete action sheet for a tapped staff card.
    [RelayCommand]
    async Task ShowActionSheet(UserCardViewModel card)
    {
        if (card is null) return;

        var sheet = new ItemActionSheet();
        sheet.Configure(
            title: card.User.FullName ?? "Staff",
            subtitle: card.User.Role ?? string.Empty,
            options: new[]
            {
                new ActionSheetOption
                {
                    Icon = "\ue3c9",  // edit
                    Label = "Edit",
                    Subtitle = "Update staff information",
                    IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                    IconColor = Color.FromArgb("#2E7D32"),
                    OnTapped = async () =>
                        await Shell.Current.GoToAsync($"{nameof(AddUserPage)}?UserId={card.User.UserID}"),
                },
                new ActionSheetOption
                {
                    Icon = "\ue872",  // delete
                    Label = "Delete",
                    Subtitle = "Hide from staff list",
                    LabelColor = Colors.Crimson,
                    IconBackgroundColor = Color.FromArgb("#FFEBEE"),
                    IconColor = Colors.Crimson,
                    OnTapped = async () => await SoftDeleteUserAsync(card),
                },
            });

        await sheet.ShowAsync();
    }

    // Confirms with the user, then soft-deletes the staff record and removes it from the list.
    private async Task SoftDeleteUserAsync(UserCardViewModel card)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Remove Staff",
            $"Remove \"{card.User.FullName}\" from the staff list?",
            "Remove", "Cancel");

        if (!confirm) return;

        await _db.DeleteUser(card.User); // now soft deletes

        var existing = Users.FirstOrDefault(u => u.User.UserID == card.User.UserID);
        if (existing is not null)
            Users.Remove(existing);
    }

    // Navigates to the Add Staff form.
    [RelayCommand]
    async Task GoToAddUser() =>
        await Shell.Current.GoToAsync(nameof(AddUserPage));
}

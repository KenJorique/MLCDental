using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Services;
using ClinicApp.Views.Shared;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.UsersRelated;

public partial class UserViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly SupabaseDataService _supabaseData;
    private readonly SupabaseRealtimeService _realtime;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;

    public ObservableCollection<UserCardViewModel> Users { get; set; } = new();

    // Injects the local, Supabase, and realtime services.
    public UserViewModel(DatabaseService db, SupabaseDataService supabaseData, SupabaseRealtimeService realtime)
    {
        _db = db;
        _supabaseData = supabaseData;
        _realtime = realtime;

        // Another device adding/editing/removing a staff account shows up here live, same as patients elsewhere.
        _realtime.OnUserChanged += async () => await LoadUsers();
    }

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

    // Set once StartSupabaseSyncAsync has run, so it doesn't repeat every OnAppearing.
    private bool _syncStarted = true;

    // Pulls remote users and backfills local Supabase IDs.
    public async Task StartSupabaseSyncAsync()
    {
        if (_syncStarted) return;
        _syncStarted = true;

        try
        {
            var remoteUsers = await _supabaseData.GetUsersAsync();
            await _db.BackfillUserSupabaseIds(remoteUsers);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserSync] {ex.Message}");
        }
    }

    // Loads the staff list from local SQLite.
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

    // Confirms, then soft-deletes the user locally and on Supabase, and logs it.
    private async Task SoftDeleteUserAsync(UserCardViewModel card)
    {
        bool confirm = await ShowConfirmAsync(
            "Remove Staff",
            $"Remove \"{card.User.FullName}\" from the staff list?",
            "Remove", Colors.Crimson);

        if (!confirm) return;

        await _db.DeleteUser(card.User); // now soft deletes locally

        // Mirror the delete to Supabase, matching by SupabaseId or username, so it doesn't resurrect on next sync.
        var supabaseId = card.User.SupabaseId;
        if (string.IsNullOrEmpty(supabaseId) && !string.IsNullOrEmpty(card.User.Username))
        {
            var remoteUsers = await _supabaseData.GetUsersAsync();
            var match = remoteUsers.FirstOrDefault(u =>
                string.Equals(u.Username, card.User.Username, StringComparison.OrdinalIgnoreCase));
            supabaseId = match?.Id;
        }

        bool remoteOk = true;
        if (!string.IsNullOrEmpty(supabaseId))
        {
            remoteOk = await _supabaseData.SoftDeleteUserAsync(supabaseId);
        }

        if (!remoteOk)
        {
            await ShowErrorAsync($"\"{card.User.FullName}\" was removed locally, but the cloud update failed — it may reappear after the next sync. Check your connection and try again.");
        }

        await _supabaseData.LogActivityAsync("UserDeleted", $"{card.User.FullName} was removed from staff");

        var existing = Users.FirstOrDefault(u => u.User.UserID == card.User.UserID);
        if (existing is not null)
            Users.Remove(existing);
    }

    // Opens the Add Staff page.
    [RelayCommand]
    async Task GoToAddUser() =>
        await Shell.Current.GoToAsync(nameof(AddUserPage));
}

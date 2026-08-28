using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.UsersRelated;

// Wraps a User with a computed status color for the active/inactive indicator dot
public partial class UserCardViewModel : ObservableObject
{
    public User User { get; }

    // Wraps the given user record for display in a list card.
    public UserCardViewModel(User user)
    {
        User = user;
    }

    // Returns StatusGood (green) if active, StatusOut (red) if inactive —
    public Color StatusColor
    {
        get
        {
            var key = User.IsActive ? "StatusGood" : "StatusOut";
            if (Application.Current?.Resources.TryGetValue(key, out var color) == true)
                return (Color)color;

            return User.IsActive ? Colors.Green : Colors.Red; // fallback
        }
    }

    // Returns "Active" or "Inactive" label text
    public string StatusText => User.IsActive ? "Active" : "Inactive";
}

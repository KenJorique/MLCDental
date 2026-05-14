using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.UsersRelated;

// Wraps a User with a computed status color for the active/inactive indicator dot
public partial class UserCardViewModel : ObservableObject
{
    public User User { get; }

    public UserCardViewModel(User user)
    {
        User = user;
    }

    // Returns green if active, red if inactive — used for the status dot color
    public Color StatusColor => User.IsActive ? Colors.Green : Colors.Red;

    // Returns "Active" or "Inactive" label text
    public string StatusText => User.IsActive ? "Active" : "Inactive";
}

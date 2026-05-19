using ClinicApp.Views.ServicesRelated;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.UsersRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels;

public partial class MenuViewModel : ObservableObject
{
    [RelayCommand]
    async Task GoToServices()
    {
        await Shell.Current.GoToAsync(nameof(ServiceListPage));
    }

    [RelayCommand]
    async Task GoToUsers()
    {
        await Shell.Current.GoToAsync(nameof(UserListPage));
    }

    [RelayCommand]
    async Task GoToSupply()
    {
        await Shell.Current.GoToAsync(nameof(SupplyListPage));
    }
}

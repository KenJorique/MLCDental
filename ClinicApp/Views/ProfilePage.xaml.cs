using ClinicApp.ViewModels;

namespace ClinicApp.Views;

public partial class ProfilePage : ContentPage
{
    readonly ProfileViewModel _vm;

    // Injects the profile ViewModel.
    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Reloads the signed-in user's info every time the page appears.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}

using ClinicApp.ViewModels.UsersRelated;

namespace ClinicApp.Views.UsersRelated;

public partial class UserListPage : ContentPage
{
    UserViewModel _viewModel;

    public UserListPage(UserViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.Delay(100);

        if (BindingContext is UserViewModel vm)
        {
            _ = Task.Run(async () => await vm.LoadUsers());
        }
    }
}

using ClinicApp.ViewModels;

namespace ClinicApp.Views.UsersRelated;

public partial class UserListPage : ContentPage
{
    UserViewModel _viewModel;

    public UserListPage(UserViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadUsersCommand.Execute(null);
    }
}
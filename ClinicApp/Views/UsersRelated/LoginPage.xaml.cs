using ClinicApp.ViewModels.UsersRelated;
using ClinicApp.Services;

namespace ClinicApp.Views.UsersRelated;

public partial class LoginPage : ContentPage
{
    // Injects the login ViewModel.
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    // Blocks the hardware/gesture back button on the login screen.
    protected override bool OnBackButtonPressed() => true;

    // Lets tapping the "Remember me" text toggle the checkbox too, not just the checkbox itself.
    private void OnRememberMeLabelTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is LoginViewModel vm)
            vm.RememberMe = !vm.RememberMe;
    }
}

using ClinicApp.ViewModels.UsersRelated;
using ClinicApp.Services;

namespace ClinicApp.Views.UsersRelated;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed() => true;

    // ?? NEW: makes tapping the "Remember me..." text toggle the checkbox
    // too, not just the small checkbox square itself. ??
    private void OnRememberMeLabelTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is LoginViewModel vm)
            vm.RememberMe = !vm.RememberMe;
    }
}

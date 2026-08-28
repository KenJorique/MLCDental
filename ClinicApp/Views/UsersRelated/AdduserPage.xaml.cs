using ClinicApp.ViewModels.UsersRelated;

namespace ClinicApp.Views.UsersRelated;

public partial class AddUserPage : ContentPage
{
    public AddUserPage(AddUserViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is AddUserViewModel vm)
        {
            vm.CancelCommand.Execute(null);
            return true; // we own navigation now
        }

        return base.OnBackButtonPressed();
    }
}

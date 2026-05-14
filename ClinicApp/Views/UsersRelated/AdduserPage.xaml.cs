using ClinicApp.ViewModels.UsersRelated;

namespace ClinicApp.Views.UsersRelated;

public partial class AddUserPage : ContentPage
{
    public AddUserPage(AddUserViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

using ClinicApp.ViewModels.UsersRelated;

namespace ClinicApp.Views.UsersRelated; // Namespace must match XAML x:Class path

public partial class AddUserPage : ContentPage
{
    public AddUserPage(AddUserViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
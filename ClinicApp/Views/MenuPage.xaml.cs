using ClinicApp.ViewModels;

namespace ClinicApp.Views;

public partial class MenuPage : ContentPage
{
    public MenuPage(MenuViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

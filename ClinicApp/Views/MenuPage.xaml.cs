using ClinicApp.ViewModels;

namespace ClinicApp.Views;

public partial class MenuPage : ContentPage
{
    readonly MenuViewModel vm;
    public MenuPage(MenuViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        vm.OnAppearing(); // ← call the ViewModel method manually
    }
}

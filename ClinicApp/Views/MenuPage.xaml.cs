using ClinicApp.Services;
using ClinicApp.ViewModels;

namespace ClinicApp.Views;

public partial class MenuPage : ContentPage
{
    readonly MenuViewModel vm;

    public MenuPage(MenuViewModel vm)
    {
        InitializeComponent();
        BindingContext = this.vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Defer the execution until the UI thread finishes rendering this layout pass
        Dispatcher.Dispatch(() =>
        {
            vm.OnAppearing();
        });
    }

    
}
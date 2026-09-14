using ClinicApp.ViewModels;

namespace ClinicApp.Views;

public partial class HomePage : ContentPage
{
    readonly HomeViewModel vm;

    // Injects the Home ViewModel and sets it as this page's binding context.
    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = this.vm = vm;
    }

    // Refreshes every section each time the Home tab becomes visible.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(() => vm.LoadCommand.Execute(null));
    }
}

using ClinicApp.ViewModels;

namespace ClinicApp.Views;

public partial class ActivityLogPage : ContentPage
{
    readonly ActivityLogViewModel vm;

    // Injects the ViewModel and sets it as this page's binding context.
    public ActivityLogPage(ActivityLogViewModel vm)
    {
        InitializeComponent();
        BindingContext = this.vm = vm;
    }

    // Loads the full activity list each time this page is shown.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(() => vm.LoadCommand.Execute(null));
    }
}

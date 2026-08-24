using ClinicApp.ViewModels;

namespace ClinicApp.Views.ReportRelated;

public partial class ReportsPage : ContentPage
{
    readonly ReportsViewModel vm;

    public ReportsPage(ReportsViewModel vm)
    {
        InitializeComponent();
        BindingContext = this.vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(() => vm.OnAppearing());
    }
}
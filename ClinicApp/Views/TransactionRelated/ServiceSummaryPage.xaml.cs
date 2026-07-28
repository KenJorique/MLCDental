using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class ServiceSummaryPage : ContentPage
{
    readonly ServiceSummaryViewModel _vm;

    public ServiceSummaryPage(ServiceSummaryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadDraft();
    }
}
using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;


public partial class BillDetailsPage : ContentPage
{
    readonly BillDetailsViewModel _vm;

    // Wires up the view model as the binding context.
    public BillDetailsPage(BillDetailsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    // Reloads the bill, items, and payments each time the page becomes visible.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _vm.LoadAsync();
    }

}
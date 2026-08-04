using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;


public partial class BillDetailsPage : ContentPage
{
	readonly BillDetailsViewModel _vm;
    public BillDetailsPage(BillDetailsViewModel vm  )
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _vm.LoadAsync();
    }

}
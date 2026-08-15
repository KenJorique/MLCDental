using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class BalanceManagementPage : ContentPage
{
    readonly BalanceManagementViewModel _vm;

    public BalanceManagementPage(BalanceManagementViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadBalancesAsync();
    }
}
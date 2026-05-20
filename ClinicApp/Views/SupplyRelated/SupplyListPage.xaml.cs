using ClinicApp.ViewModels.SupplyVM;

namespace ClinicApp.Views.SupplyRelated;

public partial class SupplyListPage : ContentPage
{
    public SupplyListPage(SupplyListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SupplyListViewModel vm)
            MainThread.BeginInvokeOnMainThread(async () =>
                await vm.LoadSuppliesCommand.ExecuteAsync(null));
    }
}

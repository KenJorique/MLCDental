using ClinicApp.ViewModels.SupplyVM;

namespace ClinicApp.Views.SupplyRelated;

public partial class StockHistoryPage : ContentPage
{
    public StockHistoryPage(StockHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is StockHistoryViewModel vm)
            vm.LoadCommand.ExecuteAsync(null);
    }
}

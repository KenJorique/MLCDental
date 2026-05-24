using ClinicApp.ViewModels.SupplyVM;

namespace ClinicApp.Views.SupplyRelated;

public partial class ReduceStockPage : ContentPage
{
    public ReduceStockPage(ReduceStockViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

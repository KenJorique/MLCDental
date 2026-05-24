using ClinicApp.ViewModels.SupplyVM;

namespace ClinicApp.Views.SupplyRelated;

public partial class AddStockPage : ContentPage
{
    public AddStockPage(AddStockViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

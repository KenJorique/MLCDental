using ClinicApp.ViewModels.SupplyVM;

namespace ClinicApp.Views.SupplyRelated;

public partial class AddSupplyPage : ContentPage
{
    public AddSupplyPage(AddSupplyViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

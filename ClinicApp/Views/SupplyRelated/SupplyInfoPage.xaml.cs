using ClinicApp.ViewModels.SupplyVM;

namespace ClinicApp.Views.SupplyRelated;

public partial class SupplyInfoPage : ContentPage
{
    public SupplyInfoPage(SupplyInfoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SupplyInfoViewModel vm)
            vm.LoadCommand.ExecuteAsync(null);
    }
}

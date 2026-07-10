using ClinicApp.ViewModels.SupplyVM;

namespace ClinicApp.Views.SupplyRelated;

public partial class SupplyListPage : ContentPage
{
    public SupplyListPage(SupplyListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.Delay(100);

        if (BindingContext is SupplyListViewModel vm)
        {
            _ = Task.Run(async () => await vm.LoadSuppliesAsync());
        }
    }
}

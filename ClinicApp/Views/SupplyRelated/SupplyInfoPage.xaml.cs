using ClinicApp.ViewModels.SupplyVM;
using The49.Maui.BottomSheet;

namespace ClinicApp.Views.SupplyRelated;

public partial class SupplyInfoPage : ContentPage
{
    private readonly AdjustStockSheet _sheet;

    public SupplyInfoPage(SupplyInfoViewModel vm, AdjustStockSheet sheet)
    {
        InitializeComponent();
        BindingContext = vm;
        _sheet = sheet;

        // Wire up sheet callbacks to VM commands
        _sheet.OnAddStock = async () => await vm.GoToAddStockCommand.ExecuteAsync(null);
        _sheet.OnReduceStock = async () => await vm.GoToReduceStockCommand.ExecuteAsync(null);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SupplyInfoViewModel vm)
            vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnAdjustStockClicked(object? sender, EventArgs e)
    {
        await _sheet.ShowAsync();
    }
}

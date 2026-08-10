using ClinicApp.ViewModels.TransactionVM;
using ClinicApp.Services;

namespace ClinicApp.Views.TransactionRelated;

public partial class BillSummaryPage : ContentPage
{
    readonly BillSummaryViewModel _vm;

    public BillSummaryPage(BillSummaryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadDraft();
    }

    void DiscountChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;

        switch (picker.SelectedIndex)
        {
            case 1:
            case 2:
                _vm.IsSpecialDiscount = false;
                _vm.DiscountPercent = 0.20m;
                break;

            case 3:
                _vm.IsSpecialDiscount = true;
                _vm.DiscountPercent = 0m;
                break;

            default:
                _vm.IsSpecialDiscount = false;
                _vm.DiscountPercent = 0m;
                break;
        }
    }
}
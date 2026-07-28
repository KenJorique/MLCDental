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
                _vm.DiscountPercent = 0.20m;
                break;

            default:
                _vm.DiscountPercent = 0m;
                break;
        }
    }

    void InstallmentMonthsChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;

        _vm.InstallmentMonths = picker.SelectedIndex switch
        {
            0 => 1,
            1 => 3,
            2 => 6,
            3 => 12,
            _ => 3
        };
    }
}
using ClinicApp.ViewModels.TransactionVM;
using ClinicApp.Views.Shared;
using CommunityToolkit.Maui.Views;

namespace ClinicApp.Views.TransactionRelated;

public partial class BillSummaryPage : ContentPage
{
    readonly BillSummaryViewModel _vm;

    public BillSummaryPage(BillSummaryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        // Resets the Picker to "None" if a discount becomes disallowed, keeping its visible selection in sync.
        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BillSummaryViewModel.CanApplyDiscount)
                && !_vm.CanApplyDiscount
                && DiscountPicker.SelectedIndex != 0)
            {
                DiscountPicker.SelectedIndex = 0;
            }
        };
    }

    // Reloads the draft each time the page appears.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadDraft();
    }

    // Closes the follow-up sheet if it's still open when leaving this page.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _ = _vm.CloseFollowUpSheetAsync();
    }

    // Reverts an invalid discount pick back to "None" and explains why, since the Picker itself stays always-enabled.
    async void DiscountChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;

        if (!_vm.CanApplyDiscount)
        {
            bool wasRealChange = picker.SelectedIndex != 0;

            _vm.IsSpecialDiscount = false;
            _vm.DiscountPercent = 0m;

            if (wasRealChange)
            {
                // Re-enters DiscountChanged once, harmlessly, since SelectedIndex is already 0 by then.
                picker.SelectedIndex = 0;

                await this.ShowPopupAsync(new ConfirmationPopup(
                    "Discount Unavailable",
                    "All services are installment-eligible.",
                    "OK", showCancelButton: false));
            }

            return;
        }

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
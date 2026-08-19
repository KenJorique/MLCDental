using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class BillSummaryPage : ContentPage
{
    readonly BillSummaryViewModel _vm;

    public BillSummaryPage(BillSummaryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        // The Picker isn't data-bound to a selected value (only
        // SelectedIndexChanged is wired up), so disabling it via
        // CanApplyDiscount doesn't reset what it's showing. Without this,
        // a discount picked before an installment item was added/toggled
        // would stay visually selected (just grayed out), and would
        // silently re-apply if that installment item is later removed —
        // this keeps the visible selection in sync with reality.
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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadDraft();
    }

    // Guards the actual discount choice. The Picker stays a normal,
    // always-enabled dropdown (an overlay/disabled-look approach here
    // looked bad) — so this handler itself is what blocks an invalid pick:
    // if the bill doesn't currently allow a discount, revert the selection
    // back to "None" and explain why, instead of letting it stick.
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
                // Setting this re-enters DiscountChanged once more, but by
                // then SelectedIndex is already 0, so it just falls through
                // without re-triggering the alert.
                picker.SelectedIndex = 0;

                await Shell.Current.DisplayAlert(
                    "Discount Unavailable",
                    "All services are installment-eligible.",
                    "OK");
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
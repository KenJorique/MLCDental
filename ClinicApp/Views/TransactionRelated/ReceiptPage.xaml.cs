using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class ReceiptPage : ContentPage
{
    readonly ReceiptViewModel _vm;

    // Wires up the view model as the binding context.
    public ReceiptPage(ReceiptViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    // Blocks the hardware/gesture back button — Done is meant to be the only way off this page.
    protected override bool OnBackButtonPressed()
    {
        return true;
    }
}

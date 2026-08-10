using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class ReceiptPage : ContentPage
{
    readonly ReceiptViewModel _vm;

    public ReceiptPage(ReceiptViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    // Blocks Android's hardware/gesture back button. Hiding the visual
    // back arrow (see Shell.BackButtonBehavior in the XAML) doesn't stop
    // this on its own — Done is meant to be the only way off this page.
    protected override bool OnBackButtonPressed()
    {
        return true;
    }
}
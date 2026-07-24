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
}
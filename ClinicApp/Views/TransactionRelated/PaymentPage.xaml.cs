using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class PaymentPage : ContentPage
{
    public PaymentPage(PaymentViewModel vm)
    {
        InitializeComponent();

        BindingContext = vm;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
    }
}
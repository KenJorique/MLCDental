using ClinicApp.ViewModels.TransactionVM;
using ClinicApp.ViewModels;

namespace ClinicApp.Views
{
    public partial class TransactionPage : ContentPage
    {
        readonly TransactionViewModel _vm;

        public TransactionPage(TransactionViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is TransactionViewModel vm)
                await vm.LoadBillsAsync();
        }
    }
}
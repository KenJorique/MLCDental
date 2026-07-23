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
            try { await _vm.LoadBillsCommand.ExecuteAsync(null); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TransactionPage] {ex.Message}");
            }
        }
    }
}
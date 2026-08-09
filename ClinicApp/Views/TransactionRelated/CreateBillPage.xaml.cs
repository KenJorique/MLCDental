using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views
{
    public partial class CreateBillPage : ContentPage
    {
        readonly CreateBillViewModel _vm;

        public CreateBillPage(CreateBillViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                // Always reload services when page appears
                await _vm.LoadServicesAsync();

                // Show the added-services summary sheet — bound to the same ViewModel so
                // it reflects live SelectedServices/TotalAmount changes. Shown fresh every
                // time this page appears (first arrival, or coming back after Cancel), so
                // it reliably shows at its minimum height again on return.
                var sheet = new CreateBillSummarySheet { BindingContext = _vm };
                _vm.Sheet = sheet;
                _ = sheet.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateBillPage] {ex.Message}");
            }
        }
    }
}
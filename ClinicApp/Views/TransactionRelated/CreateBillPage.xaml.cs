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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateBillPage] {ex.Message}");
            }
        }
    }
}
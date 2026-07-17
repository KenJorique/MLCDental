using ClinicApp.ViewModels;

namespace ClinicApp.Views
{
    public partial class WalkInBookingPage : ContentPage
    {
        readonly WalkInBookingViewModel _vm;

        public WalkInBookingPage(WalkInBookingViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try { await _vm.InitializeAsync(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WalkInPage] {ex.Message}");
            }
        }
    }
}
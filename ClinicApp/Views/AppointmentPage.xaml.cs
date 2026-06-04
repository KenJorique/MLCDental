using ClinicApp.ViewModels;

namespace ClinicApp.Views
{
    public partial class AppointmentPage : ContentPage
    {
        readonly AppointmentViewModel _vm;

        public AppointmentPage(AppointmentViewModel vm)
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
                await _vm.LoadAppointments();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentPage] {ex.Message}");
            }
        }
    }
}
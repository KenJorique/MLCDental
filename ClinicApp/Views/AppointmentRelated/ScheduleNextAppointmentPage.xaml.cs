using ClinicApp.ViewModels;

namespace ClinicApp.Views.AppointmentRelated
{
    public partial class ScheduleNextAppointmentPage : ContentPage
    {
        readonly ScheduleNextAppointmentViewModel _vm;

        public ScheduleNextAppointmentPage(ScheduleNextAppointmentViewModel vm)
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
                System.Diagnostics.Debug.WriteLine($"[ScheduleNextAppointmentPage] {ex.Message}");
            }
        }

        void OnDateSelected(object sender, DateChangedEventArgs e)
        {
            _ = _vm.LoadSlotsForDateAsync(e.NewDate);
        }
    }
}
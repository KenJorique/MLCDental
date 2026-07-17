using ClinicApp.ViewModels;

namespace ClinicApp.Views.AppointmentRelated
{
    public partial class ReschedulePage : ContentPage
    {
        readonly RescheduleViewModel _vm;

        public ReschedulePage(RescheduleViewModel vm)
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
                    $"[ReschedulePage] {ex.Message}");
            }
        }

        private async void OnDateSelected(object sender, DateChangedEventArgs e)
        {
            try { await _vm.LoadSlotsForDateAsync(e.NewDate); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ReschedulePage] DateSelected: {ex.Message}");
            }
        }
    }
}
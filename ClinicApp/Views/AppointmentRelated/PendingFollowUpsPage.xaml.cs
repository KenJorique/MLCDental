using ClinicApp.ViewModels;

namespace ClinicApp.Views.AppointmentRelated
{
    public partial class PendingFollowUpsPage : ContentPage
    {
        readonly PendingFollowUpsViewModel _vm;

        public PendingFollowUpsPage(PendingFollowUpsViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadAsync();
        }
    }
}
using ClinicApp.Services;
using ClinicApp.ViewModels;

namespace ClinicApp.Views
{
    public partial class AppointmentSchedulePage : ContentPage
    {
        readonly AppointmentScheduleViewModel _vm;
        readonly SupabaseRealtimeService _realtime;
        bool _subscribed = false;

        public AppointmentSchedulePage(
            AppointmentScheduleViewModel vm,
            SupabaseRealtimeService realtime)
        {
            InitializeComponent();
            _vm = vm;
            _realtime = realtime;
            BindingContext = vm;

            // Subscribe to redraw requests
            CalendarGraphics.Drawable = vm.CalendarDrawable;
            _vm.CalendarNeedsRedraw += () => MainThread.BeginInvokeOnMainThread(() => CalendarGraphics?.Invalidate());
        }

        private void OnCalendarNeedsRedraw()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CalendarGraphics?.Invalidate();
            });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                await _vm.LoadAppointments();

                if (!_subscribed)
                {
                    _subscribed = true;
                    _realtime.OnAppointmentChanged += async () => await _vm.LoadAppointments();
                    await _realtime.SubscribeToAppointmentEntriesAsync();
                }

                // Force initial calendar draw
                if (_vm.IsCalendarView)
                    OnCalendarNeedsRedraw();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppointmentSchedulePage] {ex.Message}");
            }
            // Force redraw after load
            await Task.Delay(100); // small delay
            CalendarGraphics?.Invalidate();
        }

        private void OnCalendarTapped(object? sender, TappedEventArgs e)
        {
            try
            {
                var pos = e.GetPosition(CalendarGraphics);
                if (pos == null) return;

                var entry = _vm.CalendarDrawable.HitTest((float)pos.Value.X, (float)pos.Value.Y);
                if (entry != null)
                    _vm.SelectAppointmentCommand.Execute(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarTap] {ex.Message}");
            }
        }

        public void RefreshCalendar()
        {
            CalendarGraphics?.Invalidate();
        }
        protected override void OnDisappearing()
        {
            if (_vm != null)
                _vm.CalendarNeedsRedraw -= OnCalendarNeedsRedraw;
            base.OnDisappearing();
        }
    }
}
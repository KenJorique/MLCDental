using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    /// <summary>
    /// Backing VM for InProcedurePage — the "active visit" queue.
    /// Separated from AppointmentScheduleViewModel because in-procedure/billing
    /// patients are no longer "scheduled", they're mid-visit; mixing them into
    /// the week schedule made the list ambiguous to read at a glance.
    /// Mirrors the AppointmentDetailSheet binding contract used by
    /// AppointmentScheduleViewModel so the same bottom sheet can be reused here.
    /// </summary>
    public partial class InProcedureViewModel : ObservableObject
    {
        readonly SupabaseDataService _supabaseData;
        private string _selectedSupabaseEntryId = string.Empty;

        public ObservableCollection<AppointmentEntry> InProcedureList { get; } = new();
        public ObservableCollection<AppointmentEntry> BillingList { get; } = new();

        [ObservableProperty] private bool isRefreshing;
        [ObservableProperty] private bool isInitialLoading = true;
        [ObservableProperty] private int inProcedureCount;
        [ObservableProperty] private int billingCount;
        [ObservableProperty] private bool hasNone;

        [ObservableProperty] private AppointmentEntry? selectedAppointment;
        [ObservableProperty] private bool showDetail;
        [ObservableProperty] private bool isInProcedureTabActive = true;
        [ObservableProperty] private bool isBillingTabActive = false;

        [RelayCommand]
        void SwitchTab(string tab)
        {
            if (tab == "in-procedure")
            {
                IsInProcedureTabActive = true;
                IsBillingTabActive = false;
            }
            else if (tab == "billing")
            {
                IsInProcedureTabActive = false;
                IsBillingTabActive = true;
            }
        }

        AppointmentDetailSheet? _detailSheet;

        // ── AppointmentDetailSheet.xaml binding contract ──
        // This queue only ever holds "in-procedure" / "billing" entries, so
        // the approved/pending/cancel rows on the shared sheet stay hidden.
        public bool IsSelectedApproved => false;
        public bool IsSelectedInTransit =>
            SelectedAppointment?.Status == "in-procedure" ||
            SelectedAppointment?.Status == "billing";
        public bool IsSelectedPending => false;
        public bool CanCancel => false;
        public bool CanChangeDate => false;

        partial void OnSelectedAppointmentChanged(AppointmentEntry? value)
        {
            OnPropertyChanged(nameof(IsSelectedInTransit));
        }

        public InProcedureViewModel(SupabaseDataService supabaseData)
        {
            _supabaseData = supabaseData;
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            try
            {
                var entries = await _supabaseData.GetAppointmentEntriesAsync();

                var inProc = entries
                    .Where(e => e.Status == "in-procedure")
                    .OrderBy(e => e.AppointmentDateTime)
                    .Select(MapToEntry)
                    .ToList();

                var billing = entries
                    .Where(e => e.Status == "billing")
                    .OrderBy(e => e.AppointmentDateTime)
                    .Select(MapToEntry)
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    InProcedureList.Clear();
                    foreach (var e in inProc) InProcedureList.Add(e);

                    BillingList.Clear();
                    foreach (var e in billing) BillingList.Add(e);

                    InProcedureCount = InProcedureList.Count;
                    BillingCount = BillingList.Count;
                    HasNone = InProcedureCount == 0 && BillingCount == 0;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InProcedureViewModel.LoadAsync] {ex.Message}");
            }
            finally
            {
                IsInitialLoading = false;
            }
        }

        private static AppointmentEntry MapToEntry(SupabaseAppointmentEntry e)
        {
            var localDt = e.AppointmentDateTime.Kind == DateTimeKind.Utc
                ? e.AppointmentDateTime.ToLocalTime()
                : e.AppointmentDateTime;

            return new AppointmentEntry
            {
                SupabaseBookingId = e.SupabaseBookingId,
                PatientName = e.PatientName,
                PatientSupabaseId = e.PatientId,
                Phone = e.Phone ?? "",
                Email = e.Email ?? "",
                Notes = e.Notes ?? "",
                AppointmentDateTime = localDt.ToString("yyyy-MM-dd HH:mm:ss"),
                Status = e.Status,
                GoogleTaskId = e.GoogleTaskId ?? ""
            };
        }

        [RelayCommand]
        async Task Refresh()
        {
            IsRefreshing = true;
            try { await LoadAsync(); }
            finally { IsRefreshing = false; }
        }

        [RelayCommand]
        async Task SelectEntry(AppointmentEntry entry)
        {
            if (entry == null) return;

            SelectedAppointment = entry;
            ShowDetail = true;

            _detailSheet = new AppointmentDetailSheet { BindingContext = this };
            _ = _detailSheet.ShowAsync();

            try
            {
                var all = await _supabaseData.GetAppointmentEntriesAsync();
                var match = all.FirstOrDefault(a => a.SupabaseBookingId == entry.SupabaseBookingId);
                _selectedSupabaseEntryId = match?.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InProcedureViewModel.SelectEntry] {ex.Message}");
            }
        }

        [RelayCommand]
        async Task CloseDetail()
        {
            ShowDetail = false;
            SelectedAppointment = null;
            await CloseSheetAsync();
        }

        async Task CloseSheetAsync()
        {
            if (_detailSheet == null) return;
            var sheet = _detailSheet;
            _detailSheet = null;
            try { await sheet.DismissAsync(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InProcedureViewModel.CloseSheetAsync] {ex.Message}");
            }
        }

        [RelayCommand]
        async Task ProceedToBilling()
        {
            if (SelectedAppointment == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Start Billing",
                $"Procedure for {SelectedAppointment.PatientName} is done.\nStart billing now?",
                "Yes, proceed", "Cancel");

            if (!confirm) return;

            var appointment = SelectedAppointment;
            var supabaseEntryId = _selectedSupabaseEntryId;

            try
            {
                ShowDetail = false;
                SelectedAppointment = null;
                await CloseSheetAsync();

                await Shell.Current.GoToAsync(
                    $"{nameof(CreateBillPage)}" +
                    $"?patientId={Uri.EscapeDataString(appointment.PatientSupabaseId ?? string.Empty)}" +
                    $"&patientName={Uri.EscapeDataString(appointment.PatientName ?? string.Empty)}" +
                    $"&appointmentEntryId={Uri.EscapeDataString(supabaseEntryId ?? string.Empty)}" +
                    $"&supabaseEntryId={Uri.EscapeDataString(supabaseEntryId ?? string.Empty)}" +
                    $"&supabaseBookingId={Uri.EscapeDataString(appointment.SupabaseBookingId ?? string.Empty)}");

                // Refresh so the card disappears from "In Procedure" the moment
                // we come back (status flips to "billing" on CreateBillPage save).
                await LoadAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InProcedureViewModel.ProceedToBilling] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        [RelayCommand]
        async Task CallPatient(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                await Shell.Current.DisplayAlert("Error", "No phone number available for this patient.", "OK");
                return;
            }

            try
            {
                if (PhoneDialer.Default.IsSupported)
                    PhoneDialer.Default.Open(phoneNumber);
                else
                    await Shell.Current.DisplayAlert("Not Supported", "Phone dialing is not supported on this device.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InProcedureViewModel.CallPatient] {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Unable to open phone dialer.", "OK");
            }
        }

        // Stubs so the shared AppointmentDetailSheet never hits an unresolved
        // Command binding — these rows stay hidden (CanCancel/CanChangeDate/
        // IsSelectedApproved are all false on this queue) but MAUI still
        // resolves the binding path when the sheet's BindingContext is set.
        [RelayCommand] void SetInTransit() { }
        [RelayCommand] void RescheduleAppointment() { }
        [RelayCommand] void CancelAppointment() { }
    }
}

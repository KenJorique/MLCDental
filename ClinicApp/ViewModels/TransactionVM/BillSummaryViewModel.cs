using ClinicApp.Helpers;
using ClinicApp.Models;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Models.TransactionModels;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using static ClinicApp.Helpers.BillDraftStore;

namespace ClinicApp.ViewModels.TransactionVM;

public partial class BillSummaryViewModel : ObservableObject
{
    readonly BillingService _billing;
    readonly SupabaseDataService _supabase;

    public ObservableCollection<ServiceLineItem> Services { get; } = new();

    [ObservableProperty] string patientName = "";
    [ObservableProperty] decimal subtotal;
    [ObservableProperty] decimal discountPercent;
    [ObservableProperty] decimal discountAmount;
    [ObservableProperty] decimal total;
    [ObservableProperty] bool isInstallment;
    [ObservableProperty] bool isBusy;
    [ObservableProperty] string createdBillId = "";
    [ObservableProperty] string createdBillNumber = "";
    [ObservableProperty] int installmentMonths = 3;
    [ObservableProperty] decimal monthlyPayment;

    public bool HasDiscount => DiscountPercent > 0;
    public bool HasInstallmentService => Services.Any(x => x.IsInstallmentEligible);
    public bool HasServices => Services.Count > 0;
    public int TotalItems => Services.Count;

    public string InstallmentSummary =>
        IsInstallment && InstallmentMonths > 0
            ? $"{InstallmentMonths} months @ ₱{MonthlyPayment:N2}/month"
            : string.Empty;

    public string SubtotalDisplay => $"₱{Subtotal:N2}";
    public string DiscountDisplay => $"₱{DiscountAmount:N2}";
    public string TotalDisplay => $"₱{Total:N2}";

    // ── Follow-up detection ──────────────────────────────────────
    public ObservableCollection<SupabaseTreatmentSequence> PendingFollowUps { get; } = new();
    [ObservableProperty] bool showFollowUpSheet;

    FollowUpRequiredSheet? _followUpSheet;
    SupabaseBill? _pendingNavBill;
    BillDraft? _pendingNavDraft;

    public BillSummaryViewModel(BillingService billing, SupabaseDataService supabase)
    {
        _billing = billing;
        _supabase = supabase;
        LoadDraft();
    }

    public void LoadDraft()
    {
        if (BillDraftStore.Current == null)
            return;

        var draft = BillDraftStore.Current;

        PatientName = draft.PatientName;
        IsInstallment = draft.IsInstallment;
        InstallmentMonths = draft.InstallmentMonths > 0 ? draft.InstallmentMonths : 3;

        Services.Clear();
        foreach (var item in draft.Services)
            Services.Add(item);

        CalculateTotals();
    }

    partial void OnIsInstallmentChanged(bool value) => CalculateTotals();
    partial void OnInstallmentMonthsChanged(int value) => CalculateTotals();

    partial void OnDiscountPercentChanged(decimal value)
    {
        if (BillDraftStore.Current != null)
            BillDraftStore.Current.DiscountPercent = value;

        CalculateTotals();
    }

    private void CalculateTotals()
    {
        Subtotal = Services.Sum(x => x.Subtotal);
        DiscountAmount = Math.Round(Subtotal * DiscountPercent, 2);
        Total = Subtotal - DiscountAmount;

        if (IsInstallment && InstallmentMonths > 0)
            MonthlyPayment = Math.Round(Total / InstallmentMonths, 2);
        else
            MonthlyPayment = 0;

        if (BillDraftStore.Current != null)
        {
            BillDraftStore.Current.Subtotal = Subtotal;
            BillDraftStore.Current.DiscountPercent = DiscountPercent;
            BillDraftStore.Current.DiscountAmount = DiscountAmount;
            BillDraftStore.Current.Total = Total;
            BillDraftStore.Current.IsInstallment = IsInstallment;
            BillDraftStore.Current.InstallmentMonths = IsInstallment ? InstallmentMonths : 0;
            BillDraftStore.Current.MonthlyPayment = MonthlyPayment;
        }

        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(InstallmentSummary));
        OnPropertyChanged(nameof(HasInstallmentService));
        OnPropertyChanged(nameof(HasServices));
        OnPropertyChanged(nameof(TotalItems));
        OnPropertyChanged(nameof(HasDiscount));
        ProceedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    async Task RemoveService(ServiceLineItem item)
    {
        if (item == null) return;

        bool confirm = await Shell.Current.CurrentPage.DisplayAlert(
            "Remove Service",
            $"Remove \"{item.ServiceName}\" from this bill?",
            "Remove", "Cancel");

        if (!confirm) return;

        Services.Remove(item);
        BillDraftStore.Current?.Services.Remove(item);
        CalculateTotals();
    }

    [RelayCommand]
    async Task Back() => await Shell.Current.GoToAsync("..");

    bool CanProceed() => HasServices && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanProceed))]
    async Task Proceed()
    {
        if (BillDraftStore.Current == null)
            return;

        IsBusy = true;
        ProceedCommand.NotifyCanExecuteChanged();

        try
        {
            var draft = BillDraftStore.Current;

            var result = await _billing.CreateBillAsync(
                draft,
                draft.AppointmentEntryId,
                draft.SupabaseEntryId);

            if (!result.Success)
            {
                await Shell.Current.DisplayAlert(
                    "Billing Error",
                    result.ErrorMessage ?? "Unable to create the bill. Please try again.",
                    "OK");
                return;
            }

            if (result.Bill == null)
            {
                await Shell.Current.DisplayAlert(
                    "Billing Error",
                    "Bill was not returned from Supabase.",
                    "OK");
                return;
            }

            CreatedBillStore.Current = result.Bill;

            // ── Auto-deduct linked supplies for every service on this bill ──
            var lowStockItems = new List<string>();
            foreach (var service in draft.Services)
            {
                var (_, insufficient) = await _supabase.DeductSuppliesForServiceAsync(
                    service.ServiceId, draft.PatientId, draft.PatientName, service.Quantity);
                lowStockItems.AddRange(insufficient);
            }

            if (lowStockItems.Count > 0)
            {
                await Shell.Current.DisplayAlert(
                    "Low Stock Warning",
                    $"These items are now low/out of stock: {string.Join(", ", lowStockItems.Distinct())}",
                    "OK");
            }

            // ── Detect any service that requires another treatment session ──
            var newlyOpenedFollowUps = new List<SupabaseTreatmentSequence>();
            foreach (var line in draft.Services)
            {
                var service = await _supabase.GetServiceByIdAsync(line.ServiceId);
                if (service == null || !service.RequiresMultipleSessions)
                    continue;

                var sequence = await _supabase.RecordCompletedSessionAsync(
                    draft.PatientId, draft.PatientName, service, draft.SupabaseBookingId);

                if (sequence != null && sequence.Status == "awaiting_schedule")
                    newlyOpenedFollowUps.Add(sequence);
            }

            if (newlyOpenedFollowUps.Count > 0)
            {
                // Hold the payment-page navigation until staff dismisses the follow-up sheet
                _pendingNavBill = result.Bill;
                _pendingNavDraft = draft;

                PendingFollowUps.Clear();
                foreach (var f in newlyOpenedFollowUps)
                    PendingFollowUps.Add(f);

                _followUpSheet = new FollowUpRequiredSheet { BindingContext = this };
                ShowFollowUpSheet = true;
                await _followUpSheet.ShowAsync();
                return;
            }

            await GoToPaymentAsync(result.Bill, draft);
        }
        finally
        {
            IsBusy = false;
            ProceedCommand.NotifyCanExecuteChanged();
        }
    }

    async Task GoToPaymentAsync(SupabaseBill bill, BillDraft draft)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(PaymentPage)}" +
            $"?billId={bill.Id}" +
            $"&patientId={Uri.EscapeDataString(bill.PatientId)}" +
            $"&patientName={Uri.EscapeDataString(bill.PatientName)}" +
            $"&appointmentEntryId={Uri.EscapeDataString(draft.AppointmentEntryId ?? string.Empty)}" +
            $"&supabaseEntryId={Uri.EscapeDataString(draft.SupabaseEntryId ?? string.Empty)}" +
            $"&supabaseBookingId={Uri.EscapeDataString(draft.SupabaseBookingId ?? string.Empty)}");
    }

    async Task CloseFollowUpSheetAsync()
    {
        if (_followUpSheet == null) return;
        var sheet = _followUpSheet;
        _followUpSheet = null;
        try { await sheet.DismissAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BillSummary] CloseFollowUpSheet: {ex.Message}"); }
    }

    /// Staff picks a specific pending session to schedule right now.
    [RelayCommand]
    async Task ScheduleFollowUpNow(SupabaseTreatmentSequence sequence)
    {
        if (sequence == null) return;

        ShowFollowUpSheet = false;
        await CloseFollowUpSheetAsync();

        var draft = BillDraftStore.Current;

        await Shell.Current.GoToAsync(
            $"{nameof(ScheduleNextAppointmentPage)}" +
            $"?sequenceId={Uri.EscapeDataString(sequence.Id)}" +
            $"&patientId={Uri.EscapeDataString(sequence.PatientId)}" +
            $"&patientName={Uri.EscapeDataString(sequence.PatientName)}" +
            $"&phone={Uri.EscapeDataString(draft?.Phone ?? string.Empty)}" +
            $"&email={Uri.EscapeDataString(string.Empty)}" +
            $"&serviceId={Uri.EscapeDataString(sequence.ServiceId)}" +
            $"&serviceName={Uri.EscapeDataString(sequence.ServiceName)}" +
            $"&sessionNumber={sequence.SessionNumber + 1}" +
            $"&totalSessions={sequence.TotalSessions}" +
            $"&recommendedDate={Uri.EscapeDataString(sequence.RecommendedDate?.ToString("o") ?? string.Empty)}");

        PendingFollowUps.Remove(sequence);
        if (PendingFollowUps.Count == 0 && _pendingNavBill != null && _pendingNavDraft != null)
            await GoToPaymentAsync(_pendingNavBill, _pendingNavDraft);
    }

    /// Staff defers scheduling — the sequence stays "awaiting_schedule" and will show up
    /// in the Appointment Schedule page's "Follow-ups Needed" banner.
    [RelayCommand]
    async Task ContinueToPayment()
    {
        ShowFollowUpSheet = false;
        await CloseFollowUpSheetAsync();

        if (_pendingNavBill != null && _pendingNavDraft != null)
            await GoToPaymentAsync(_pendingNavBill, _pendingNavDraft);
    }
}
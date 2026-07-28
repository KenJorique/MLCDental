using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.TransactionRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static ClinicApp.Helpers.BillDraftStore;

namespace ClinicApp.ViewModels.TransactionVM;

[QueryProperty(nameof(BillId), "billId")]
[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
[QueryProperty(nameof(AppointmentEntryId), "appointmentEntryId")]
[QueryProperty(nameof(SupabaseEntryId), "supabaseEntryId")]
[QueryProperty(nameof(SupabaseBookingId), "supabaseBookingId")]
public partial class PaymentViewModel : ObservableObject
{
    private readonly SupabaseDataService _supabase;

    public PaymentViewModel(SupabaseDataService supabase)
    {
        _supabase = supabase;
    }

    [ObservableProperty]
    private string billId = string.Empty;

    [ObservableProperty]
    private string patientId = string.Empty;
    [ObservableProperty] string appointmentEntryId = string.Empty;
    [ObservableProperty] string supabaseEntryId = string.Empty;
    [ObservableProperty] string supabaseBookingId = string.Empty;

    [ObservableProperty]
    private string patientName = string.Empty;

    [ObservableProperty]
    private decimal paymentAmount;

    [ObservableProperty]
    private SupabaseBill? bill;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public string DueDateDisplay =>
    Bill?.DueDateDisplay ?? "—";

    public string LastPaymentDateDisplay =>
        Bill?.LastPaymentDateDisplay ?? "—";

    partial void OnBillIdChanged(string value)
    {
        if (CreatedBillStore.Current?.Id == value)
        {
            Bill = CreatedBillStore.Current;
            CreatedBillStore.Current = null;
            return;
        }
        if (!string.IsNullOrWhiteSpace(value))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await LoadBillAsync());
        }
    }

    partial void OnBillChanged(SupabaseBill? value)
    {
        PaymentAmount = value?.Balance ?? 0;

        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(PaidDisplay));
        OnPropertyChanged(nameof(BalanceDisplay));
        OnPropertyChanged(nameof(BillNumber));
        OnPropertyChanged(nameof(DueDateDisplay));
        OnPropertyChanged(nameof(LastPaymentDateDisplay));
    }

    private async Task LoadBillAsync()
    {
        IsBusy = true;

        try
        {
            Bill = await _supabase.GetBillByIdAsync(BillId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public string BillNumber =>
        Bill?.BillNumber ?? "";

    public string SubtotalDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.Subtotal:N2}";

    public string DiscountDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.DiscountAmount:N2}";

    public string TotalDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.TotalAmount:N2}";

    public string PaidDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.AmountPaid:N2}";

    public string BalanceDisplay =>
        Bill == null ? "₱0.00" : $"₱{Bill.Balance:N2}";

    [RelayCommand]
    private async Task RecordPayment()
    {
        if (Bill == null)
            return;

        IsBusy = true;
        HasError = false;

        try
        {
            if (PaymentAmount <= 0)
            {
                await Shell.Current.GoToAsync(
     $"{nameof(ReceiptPage)}" +
     $"?billId={Bill.Id}" +
     $"&patientName={Uri.EscapeDataString(PatientName)}" +
     $"&patientId={Uri.EscapeDataString(PatientId)}" +
     $"&appointmentEntryId={Uri.EscapeDataString(AppointmentEntryId)}" +
     $"&supabaseEntryId={Uri.EscapeDataString(SupabaseEntryId)}" +
     $"&supabaseBookingId={Uri.EscapeDataString(SupabaseBookingId)}");
                return;
            }

            var (success, error) =
                await _supabase.RecordPaymentAsync(Bill.Id, PaymentAmount);

            if (!success)
            {
                HasError = true;
                ErrorMessage = error ?? "Failed to record payment.";
                return;
            }

            await Shell.Current.GoToAsync(
    $"{nameof(ReceiptPage)}" +
    $"?billId={Bill.Id}" +
    $"&patientName={Uri.EscapeDataString(PatientName)}" +
    $"&patientId={Uri.EscapeDataString(PatientId)}" +
    $"&appointmentEntryId={Uri.EscapeDataString(AppointmentEntryId)}" +
    $"&supabaseEntryId={Uri.EscapeDataString(SupabaseEntryId)}" +
    $"&supabaseBookingId={Uri.EscapeDataString(SupabaseBookingId)}");
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SkipPayment()
    {
        if (Bill == null)
            return;

        await Shell.Current.GoToAsync(
    $"{nameof(ReceiptPage)}" +
    $"?billId={Bill.Id}" +
    $"&patientName={Uri.EscapeDataString(PatientName)}" +
    $"&patientId={Uri.EscapeDataString(PatientId)}" +
    $"&appointmentEntryId={Uri.EscapeDataString(AppointmentEntryId)}" +
    $"&supabaseEntryId={Uri.EscapeDataString(SupabaseEntryId)}" +
    $"&supabaseBookingId={Uri.EscapeDataString(SupabaseBookingId)}");
    }
}
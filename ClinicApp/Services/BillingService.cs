using ClinicApp.Models.PatientModels;
using ClinicApp.Models.SupabaseModels;
using ClinicApp.Models.TransactionModels;
using ClinicApp.Models.TreatmentModels;

namespace ClinicApp.Services;

public class BillingService
{
    private readonly SupabaseDataService _supabase;
    private readonly DatabaseService _database;

    // Injects the shared data service and local database.
    public BillingService(
        SupabaseDataService supabase,
        DatabaseService database)
    {
        _supabase = supabase;
        _database = database;
    }

    // Creates the bill, links it to a patient (Supabase ID, then phone, then name), and writes each service's bill item plus its treatment/tooth record.
    public async Task<BillingResult> CreateBillAsync(
     BillDraft draft,
     string? appointmentEntryId,
     string? supabaseEntryId)
    {
        var result = new BillingResult();

        try
        {
            var patientId = draft.PatientId;

            // Walk-in fallback
            if (string.IsNullOrWhiteSpace(patientId) && !string.IsNullOrWhiteSpace(draft.Phone))
            {
                var patient = await _supabase.GetPatientByPhoneAsync(draft.Phone);

                if (patient != null)
                    patientId = patient.Id;
            }

            // Name fallback — same as GetLocalPatientIdAsync below uses for treatment records,
            // so a bill can't end up orphaned from the patient while the treatment record links fine.
            if (string.IsNullOrWhiteSpace(patientId) && !string.IsNullOrWhiteSpace(draft.PatientName))
            {
                var patient = await _supabase.GetPatientByNameAsync(draft.PatientName);

                if (patient != null)
                    patientId = patient.Id;
            }

            if (string.IsNullOrWhiteSpace(patientId))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BillingService] WARNING: bill for '{draft.PatientName}' has no linked patient — " +
                    "will only show up via name matching in the ledger.");
            }
            System.Diagnostics.Debug.WriteLine(
        $"[BillingService] Creating bill — patientId='{patientId}' patientName='{draft.PatientName}'");
            var bill = new SupabaseBill
            {
                CreatedAt = DateTime.UtcNow,

                PatientId = patientId,
                PatientName = draft.PatientName,

                Subtotal = draft.Subtotal,
                DiscountPercent = draft.DiscountPercent,
                DiscountAmount = draft.DiscountAmount,
                TotalAmount = draft.Total,
                Balance = draft.Total,
                MinimumDueToday = draft.AmountDueToday,
                IsInstallment = draft.IsInstallment,
                InstallmentMonths = draft.IsInstallment ? draft.InstallmentMonths : 0,
                MonthlyPayment = draft.IsInstallment ? draft.MonthlyPayment : 0,
                InstallmentNotes = draft.InstallmentSummary,

                DueDate = draft.IsInstallment
         ? DateTime.UtcNow.AddMonths(1)
         : null,

                LastPaymentDate = null,

                AmountPaid = 0,
                Status = "unpaid",
                VisitDate = DateTime.UtcNow,
                Notes = draft.Notes
            };

            var saved = await _supabase.CreateBillAsync(bill);

            if (saved == null)
            {
                result.Success = false;
                result.ErrorMessage = "Unable to create bill.";

                return result;
            }
            var localPatientId = await GetLocalPatientIdAsync(
    draft.PatientId,
    draft.PatientName);

            // Cleanup (deleting the source appointment_entries/booking row) happens in
            // ReceiptViewModel.Done() instead of here, so an abandoned payment flow
            // doesn't remove the patient from "In Procedure" before payment is confirmed.
            result.Bill = saved;

            // Installment-eligible items are excluded from the discount entirely (matches BillSummaryViewModel.CalculateTotals); the rest share draft.DiscountAmount proportionally by subtotal.
            var discountEligibleSubtotal = draft.Services
                .Where(s => !s.IsInstallmentEligible)
                .Sum(s => s.Subtotal);

            foreach (var item in draft.Services)
            {
                // This item's proportional share of the discount — none if installment-eligible.
                var itemDiscountShare =
                    (!item.IsInstallmentEligible && discountEligibleSubtotal > 0)
                        ? Math.Round(
                            item.Subtotal / discountEligibleSubtotal * draft.DiscountAmount,
                            2)
                        : 0m;

                var billItem = new SupabaseBillItemInsert
                {
                    Id = Guid.NewGuid().ToString(),

                    BillId = saved.Id,

                    ServiceId = item.ServiceId,

                    ServiceName = item.ServiceName,

                    UnitPrice = item.UnitPrice,

                    Quantity = item.Quantity,

                    ToothNumbers =
                        string.IsNullOrWhiteSpace(item.ToothNumbers)
                        ? null
                        : item.ToothNumbers,

                    AffectsTeeth =
                        item.ShowTeethInput &&
                        item.ParsedTeethNumbers.Count > 0,

                    // Per-item installment: 50% down today, remainder over the chosen months — only when eligible AND selected.
                    IsInstallment = item.IsInstallmentEligible && item.IsInstallmentSelected,
                    InstallmentMonths = item.IsInstallmentSelected ? item.SelectedInstallmentMonths : 0,
                    DownpaymentAmount = item.DownpaymentAmount,
                    MonthlyPayment = item.MonthlyPaymentAmount,
                    // Starts at the full subtotal (the downpayment is recorded as a payment against it, not subtracted here).
                    // Eligible items are always due in full and excluded from the discount, regardless of plan selection.
                    Balance = item.IsInstallmentEligible
                        ? item.Subtotal
                        : Math.Round(item.Subtotal - itemDiscountShare, 2)
                };
                if (localPatientId > 0)
                {
                    if (item.ShowTeethInput &&
                        item.ParsedTeethNumbers.Count > 0)
                    {
                        await ApplyToothConditionsAsync(
                            localPatientId,
                            item.ServiceName,
                            item.ParsedTeethNumbers);
                    }
                    else
                    {
                        await LogGeneralServiceAsync(
                            localPatientId,
                            item.ServiceName);
                    }
                }

                await _supabase.AddBillItemAsync(billItem);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("========== BILLING ERROR ==========");
            System.Diagnostics.Debug.WriteLine(ex.ToString());
            System.Diagnostics.Debug.WriteLine("===================================");

            result.Success = false;
            result.ErrorMessage = ex.ToString();
        }

        return result;
    }



    // Logs a general (non-tooth-specific) service as a treatment history entry.
    private async Task LogGeneralServiceAsync(
    int patientId,
    string serviceName)
    {
        var history = new TreatmentHistory
        {
            PatientId = patientId,
            ToothNumber = 0,
            ToothName = "",
            Condition = serviceName,
            Description = serviceName,
            Color = "#3B82F6",
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Notes = "Service rendered",
            ActionType = "Service"
        };

        await _database.AddTreatmentHistory(history);
    }
    // Updates each tooth's chart record and logs a treatment history entry per tooth.
    private async Task ApplyToothConditionsAsync(
     int patientId,
     string serviceName,
     List<int> teethNumbers)
    {
        try
        {
            var condition = ToothAwareServices.GetCondition(serviceName);

            // Same palette as DentalChartViewModel, so history entries match the chart's color-coding.
            var hex = ClinicApp.ViewModels.DentalChart.DentalChartViewModel
                .ConditionColors.TryGetValue(condition, out var c) ? c : "#FFFFFF";

            foreach (var toothNum in teethNumbers)
            {
                // Save tooth record
                var record = new ToothRecord
                {
                    PatientId = patientId,
                    ToothNumber = toothNum,
                    Condition = condition,
                    Color = hex,
                    Notes = $"{serviceName} — {DateTime.Now:MMM dd, yyyy}",
                    DateUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                await _database.SaveToothRecord(record);

                // One treatment history entry per tooth, with the correct ToothNumber and Color.
                var history = new TreatmentHistory
                {
                    PatientId = patientId,
                    ToothNumber = toothNum,
                    ToothName = new ClinicApp.ViewModels.DentalChart.ToothViewModel
                    {
                        ToothNumber = toothNum
                    }.ToothName,
                    Condition = condition,
                    Color = hex,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Description = $"{serviceName} — Tooth #{toothNum}",
                    Notes = $"Condition applied: {condition}",
                    ActionType = "Added"
                };
                await _database.AddTreatmentHistory(history);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[Chart] Applied '{condition}' to teeth: {string.Join(", ", teethNumbers)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApplyTeeth] {ex.Message}");
        }
    }

    // Resolves the local SQLite patient ID by Supabase ID, then by name.
    private async Task<int> GetLocalPatientIdAsync(
    string patientSupabaseId,
    string patientName)
    {
        try
        {
            if (!string.IsNullOrEmpty(patientSupabaseId))
            {
                var patient = await _database.GetPatientBySupabaseId(patientSupabaseId);

                if (patient != null)
                    return patient.PatientID;
            }

            var patients = await _database.GetPatients();

            var match = patients.FirstOrDefault(p =>
                $"{p.FirstName} {p.LastName}".Trim()
                .Equals(patientName.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            return match?.PatientID ?? 0;
        }
        catch
        {
            return 0;
        }
    }

}
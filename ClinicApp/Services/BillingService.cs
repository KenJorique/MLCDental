using ClinicApp.Models;

namespace ClinicApp.Services;

public class BillingService
{
    private readonly SupabaseDataService _supabase;
    private readonly DatabaseService _database;

    public BillingService(
        SupabaseDataService supabase,
        DatabaseService database)
    {
        _supabase = supabase;
        _database = database;
    }

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
            if (string.IsNullOrWhiteSpace(patientId))
            {
                var patient = await _supabase.GetPatientByPhoneAsync(draft.Phone);

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

            // BUGFIX: this used to delete the appointment_entries row (and its
            // linked booking) right here — the moment the bill was CREATED,
            // i.e. as soon as staff tapped "Proceed to Payment" on Bill
            // Summary. That meant a patient vanished from "In Procedure" even
            // if the payment flow was abandoned/backed-out-of before ever
            // reaching Receipt. ReceiptViewModel.Done() already performs this
            // exact same cleanup at the point the flow is genuinely
            // finished — that's the only place it should happen.

            result.Bill = saved;

            // Discount base: ANY installment-ELIGIBLE item is excluded from
            // the discount, whether or not the patient actually turned the
            // plan toggle on for it (matches BillSummaryViewModel.CalculateTotals,
            // which uses the same IsInstallmentEligible-only rule). Computed
            // once here, outside the loop, and used below to give each
            // non-eligible item its proportional share of draft.DiscountAmount.
            //
            // NOTE: using draft.DiscountAmount (not draft.DiscountPercent) so
            // this works for BOTH discount types Bill Summary supports —
            // percent-based (senior/PWD) AND the flat "special discount"
            // peso amount, which has no percent at all (DiscountPercent is 0
            // in that case). Previously this only ever multiplied by
            // DiscountPercent, so a flat special discount never made it into
            // any item's Balance.
            var discountEligibleSubtotal = draft.Services
                .Where(s => !s.IsInstallmentEligible)
                .Sum(s => s.Subtotal);

            foreach (var item in draft.Services)
            {
                // This item's proportional share of the total discount.
                // Installment-eligible items get none (see above); among the
                // rest, each item's share is proportional to its own subtotal.
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

                    // Per-item installment plan — 50% down today, remainder
                    // split over the chosen number of months. Only meaningful
                    // when the service is both eligible AND the staff turned
                    // the toggle on for this specific item.
                    IsInstallment = item.IsInstallmentEligible && item.IsInstallmentSelected,
                    InstallmentMonths = item.IsInstallmentSelected ? item.SelectedInstallmentMonths : 0,
                    DownpaymentAmount = item.DownpaymentAmount,
                    MonthlyPayment = item.MonthlyPaymentAmount,
                    // Balance starts at the FULL subtotal, not just the
                    // remaining-after-downpayment — the downpayment itself
                    // still needs to be recorded as a payment against this
                    // item (see the payment-allocation note for your teammate).
                    //
                    // Eligibility (not selection) decides discount exemption:
                    // an eligible item is excluded from the discount and due
                    // in full — same whether it's on a plan or not. Only
                    // truly non-eligible items get their proportional share
                    // of draft.DiscountAmount subtracted off. Without this,
                    // the sum of per-item balances wouldn't match the bill's
                    // actual (discounted) TotalAmount whenever a discount
                    // applies.
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
    private async Task ApplyToothConditionsAsync(
     int patientId,
     string serviceName,
     List<int> teethNumbers)
    {
        try
        {
            var condition = ToothAwareServices.GetCondition(serviceName);

            // Look up the hex color for this condition, same palette
            // used by DentalChartViewModel, so history entries match
            // the chart's color-coding.
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

                // Add ONE treatment history entry PER tooth, with ToothNumber
                // and Color set correctly (previously defaulted to 0 / white).
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
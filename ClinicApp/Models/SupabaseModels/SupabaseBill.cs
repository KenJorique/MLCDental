using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
using SQLite;
using Table = Supabase.Postgrest.Attributes.TableAttribute;
using PrimaryKey = Supabase.Postgrest.Attributes.PrimaryKeyAttribute;
using Column = Supabase.Postgrest.Attributes.ColumnAttribute;
using ClinicApp.Helpers;

namespace ClinicApp.Models.SupabaseModels
{
    [Table("bills")]
    public class SupabaseBill : BaseModel
    {
        // Primary key of the bill row.
        [PrimaryKey("id")]
        public string Id { get; set; } = string.Empty;

        // Foreign key to the patient this bill belongs to.
        [Column("patient_id")]
        public string PatientId { get; set; } = string.Empty;

        // Denormalized patient name, kept on the bill for quick display.
        [Column("patient_name")]
        public string PatientName { get; set; } = string.Empty;

        // Optional link back to the appointment this bill was generated from.
        [Column("appointment_entry_id")]
        public string? AppointmentEntryId { get; set; }

        // Full amount charged for the bill before payments.
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        // Total amount the patient has paid so far.
        [Column("amount_paid")]
        public decimal AmountPaid { get; set; }

        // Remaining amount owed (TotalAmount - AmountPaid).
        [Column("balance")]
        public decimal Balance { get; set; }

        // Payment status: "unpaid", "partial", or "paid".
        [Column("status")]
        public string Status { get; set; } = "unpaid";

        // Whether this bill is being paid off via an installment plan.
        [Column("is_installment")]
        public bool IsInstallment { get; set; }

        // Free-text notes specific to the installment arrangement.
        [Column("installment_notes")]
        public string? InstallmentNotes { get; set; }

        // Date the next/current payment is due.
        [Column("due_date")]
        public DateTime? DueDate { get; set; }

        // Human-readable bill/invoice number.
        [Column("bill_number")]
        public string? BillNumber { get; set; }

        // Date of the visit this bill is associated with.
        [Column("visit_date")]
        public DateTime VisitDate { get; set; }

        // General free-text notes on the bill.
        [Column("notes")]
        public string? Notes { get; set; }

        // Timestamp the bill row was created.
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // Sum of line items before discount.
        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        // Discount applied, expressed as a percentage (e.g. 0.10 = 10%).
        [Column("discount_percent")]
        public decimal DiscountPercent { get; set; }

        // Discount applied, expressed as a fixed peso amount.
        [Column("discount_amount")]
        public decimal DiscountAmount { get; set; }

        // Number of months the installment plan runs for.
        [Column("installment_months")]
        public int InstallmentMonths { get; set; }

        // Fixed amount due each month under the installment plan.
        [Column("monthly_payment")]
        public decimal MonthlyPayment { get; set; }

        // Date of the most recent payment made against this bill.
        [Column("last_payment_date")]
        public DateTime? LastPaymentDate { get; set; }

        // Sum of what's collected THIS visit (full price for
        // non-installment items + 50% down for installment items,
        // minus discount) — computed once at bill creation on Bill
        // Summary. Distinct from Balance, which is the FULL lifetime
        // amount owed across the whole bill.
        [Column("minimum_due_today")]
        public decimal MinimumDueToday { get; set; }

        // Formatted peso display of MinimumDueToday.
        [Ignore]
        [JsonIgnore]
        public string MinimumDueTodayDisplay => $"₱{MinimumDueToday:N2}";

        // Formatted display of LastPaymentDate, or an em dash if none.
        [Ignore]
        [JsonIgnore]
        public string LastPaymentDateDisplay =>
    LastPaymentDate.HasValue
        ? LastPaymentDate.Value.ToLocalSafe().ToString("MMM dd, yyyy")
        : "—";

        // Formatted display of DueDate, or an em dash if none.
        [Ignore]
        [JsonIgnore]
        public string DueDateDisplay =>
            DueDate.HasValue
                ? DueDate.Value.ToLocalSafe().ToString("MMM dd, yyyy")
                : "—";

        // Whether a due date has been set at all.
        [Ignore]
        [JsonIgnore]
        public bool HasDueDate => DueDate.HasValue;

        // Human-readable summary of the installment plan, if any.
        [Ignore]
        [JsonIgnore]
        public string InstallmentDisplay =>
            IsInstallment && InstallmentMonths > 0
                ? $"{InstallmentMonths} months @ ₱{MonthlyPayment:N2}/month"
                : string.Empty;

        // Display helpers — no [Column] needed
        // Human-readable label for the raw Status code.
        [Ignore]
        [JsonIgnore]
        public string StatusDisplay => Status switch
        {
            "paid" => "Paid",
            "partial" => "Partial",
            "unpaid" => "Unpaid",
            _ => Status
        };

        // Text color associated with the current Status.
        [Ignore]
        [JsonIgnore]
        public Color StatusColor => Status switch
        {
            "paid" => Color.FromArgb("#2E7D32"),
            "partial" => Color.FromArgb("#E65100"),
            "unpaid" => Color.FromArgb("#C62828"),
            _ => Color.FromArgb("#888888")
        };

        // Background color associated with the current Status.
        [Ignore]
        [JsonIgnore]
        public Color StatusBgColor => Status switch
        {
            "paid" => Color.FromArgb("#E8F5E9"),
            "partial" => Color.FromArgb("#FFF3E0"),
            "unpaid" => Color.FromArgb("#FCEAEA"),
            _ => Color.FromArgb("#F5F5F5")
        };

        // Formatted peso display of TotalAmount.
        [Ignore]
        [JsonIgnore]
        public string TotalDisplay => $"₱{TotalAmount:N2}";

        // Formatted peso display of AmountPaid.
        [Ignore]
        [JsonIgnore]
        public string PaidDisplay => $"₱{AmountPaid:N2}";

        // Formatted peso display of Balance.
        [Ignore]
        [JsonIgnore]
        public string BalanceDisplay => $"₱{Balance:N2}";

        // Display value for BillNumber, or an em dash if none.
        [Ignore]
        [JsonIgnore]
        public string BillNumberDisplay => BillNumber ?? "—";

        // Formatted peso display of Subtotal.
        [Ignore]
        [JsonIgnore]
        public string SubtotalDisplay => $"₱{Subtotal:N2}";

        // Formatted peso display of DiscountAmount.
        [Ignore]
        [JsonIgnore]
        public string DiscountAmountDisplay => $"₱{DiscountAmount:N2}";

        // Formatted percentage display of DiscountPercent.
        [Ignore]
        [JsonIgnore]
        public string DiscountPercentDisplay =>
            DiscountPercent <= 0 ? "0%" : $"{DiscountPercent * 100m:N0}%";

        // Display date: VisitDate if set, otherwise falls back to CreatedAt.
        [Ignore]
        [JsonIgnore]
        public string DateDisplay =>
    VisitDate == default
        ? CreatedAt.ToLocalSafe().ToString("MMM dd, yyyy")
        : VisitDate.ToLocalSafe().ToString("MMM dd, yyyy");

        // True when there's still a balance owed and today is past the due date.
        // (Not restricted to installment bills — a one-time bill can be overdue too.)
        [Ignore]
        [JsonIgnore]
        public bool IsOverdue =>
    Balance > 0 &&
    DueDate.HasValue &&
    DateTime.Now.Date > DueDate.Value.ToLocalSafe().Date;

        // Installment-plan status text: blank for non-installment bills,
        // otherwise "Paid" / "Overdue" / "On Schedule".
        [Ignore]
        [JsonIgnore]
        public string DueStatusText =>
            !IsInstallment
                ? ""
                : Balance <= 0
                    ? "Paid"
                    : IsOverdue
                        ? "Overdue"
                        : "On Schedule";

        // Color associated with DueStatusText.
        [Ignore]
        [JsonIgnore]
        public Color DueStatusColor =>
            !IsInstallment
                ? Color.FromArgb("#6B7280")
                : Balance <= 0
                    ? Color.FromArgb("#16A34A")
                    : IsOverdue
                        ? Color.FromArgb("#DC2626")
                        : Color.FromArgb("#F59E0B");




    }
}

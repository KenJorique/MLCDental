using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;
using ClinicApp.Helpers;
using Table = Supabase.Postgrest.Attributes.TableAttribute;
using PrimaryKey = Supabase.Postgrest.Attributes.PrimaryKeyAttribute;
using Column = Supabase.Postgrest.Attributes.ColumnAttribute;

namespace ClinicApp.Models
{
    [Table("bill_items")]
    public partial class SupabaseBillItem : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = string.Empty;

        [Column("bill_id")]
        public string BillId { get; set; } = string.Empty;

        [Column("service_id")]
        public string? ServiceId { get; set; }

        [Column("service_name")]
        public string ServiceName { get; set; } = string.Empty;

        [Column("unit_price")]
        public decimal UnitPrice { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; } = 1;

        [Column("subtotal", ignoreOnUpdate: true)]
        [JsonProperty("subtotal")]
        public decimal Subtotal { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("tooth_numbers")]
        public string? ToothNumbers { get; set; }

        [Column("affects_teeth")]
        public bool AffectsTeeth { get; set; }

        // ── Per-item installment plan (requires migration_bill_items_installment.sql to be run first) ──
        [Column("is_installment")]
        public bool IsInstallment { get; set; }

        [Column("installment_months")]
        public int InstallmentMonths { get; set; }

        [Column("downpayment_amount")]
        public decimal DownpaymentAmount { get; set; }

        [Column("monthly_payment")]
        public decimal MonthlyPayment { get; set; }

        [Column("amount_paid")]
        public decimal AmountPaid { get; set; }

        [Column("balance")]
        public decimal Balance { get; set; }

        [Column("due_date")]
        public DateTime? DueDate { get; set; }

        [Column("last_payment_date")]
        public DateTime? LastPaymentDate { get; set; }


        // Display helpers
        [JsonIgnore]
        public string ToothNumbersDisplay =>
            string.IsNullOrEmpty(ToothNumbers)
                ? "" : $"Teeth: {ToothNumbers}";

        [JsonIgnore]
        public bool HasTeethInfo =>
            !string.IsNullOrEmpty(ToothNumbers);

        [JsonIgnore]
        public string SubtotalDisplay => $"₱{Subtotal:N2}";

        [JsonIgnore]
        public string UnitPriceDisplay => $"₱{UnitPrice:N2}";

        [Ignore]
        [JsonIgnore]
        public bool IsExpanded { get; set; }

        [Ignore]
        [JsonIgnore]
        public string QuantityDisplay =>
            $"Quantity: {Quantity}";

        [Ignore]
        [JsonIgnore]
        public string LineTotalDisplay =>
            $"Line Total: ₱{Subtotal:N2}";

        [Ignore]
        [JsonIgnore]
        public bool HasNotes =>
            !string.IsNullOrWhiteSpace(Notes);

        [Ignore]
        [JsonIgnore]
        public bool HasToothNumbers =>
            !string.IsNullOrWhiteSpace(ToothNumbers);

        // ── Per-item installment display helpers ──
        [JsonIgnore]
        public string DownpaymentDisplay => $"₱{DownpaymentAmount:N2}";

        [JsonIgnore]
        public string MonthlyPaymentDisplay => $"₱{MonthlyPayment:N2}";

        [JsonIgnore]
        public string BalanceDisplay => $"₱{Balance:N2}";

        [JsonIgnore]
        public string AmountPaidDisplay => $"₱{AmountPaid:N2}";

        [JsonIgnore]
        public string DueDateDisplay =>
            DueDate.HasValue ? DueDate.Value.ToLocalSafe().ToString("MMM dd, yyyy") : "—";

        [JsonIgnore]
        public string LastPaymentDateDisplay =>
            LastPaymentDate.HasValue ? LastPaymentDate.Value.ToLocalSafe().ToString("MMM dd, yyyy") : "—";

        [JsonIgnore]
        public string InstallmentDisplay =>
            IsInstallment && InstallmentMonths > 0
                ? $"{DownpaymentDisplay} down, then {MonthlyPaymentDisplay} x {InstallmentMonths} mo."
                : string.Empty;

        [JsonIgnore]
        public bool IsOverdue =>
            IsInstallment &&
            Balance > 0 &&
            DueDate.HasValue &&
            DateTime.Now.Date > DueDate.Value.ToLocalSafe().Date;

        [JsonIgnore]
        public string DueStatusText =>
            !IsInstallment
                ? ""
                : Balance <= 0
                    ? "Paid"
                    : IsOverdue
                        ? "Overdue"
                        : "On Schedule";

        [JsonIgnore]
        public Color DueStatusColorBg =>
            !IsInstallment
                ? Color.FromArgb("#6B7280")
                : Balance <= 0
                    ? Color.FromArgb("#16A34A")
                    : IsOverdue
                        ? Color.FromArgb("#DC2626")
                        : Color.FromArgb("#F59E0B");

    }
}
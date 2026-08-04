    using Supabase.Postgrest.Attributes;
    using Supabase.Postgrest.Models;
    using Newtonsoft.Json;
    using SQLite;
    using Table = Supabase.Postgrest.Attributes.TableAttribute;
    using PrimaryKey = Supabase.Postgrest.Attributes.PrimaryKeyAttribute;
    using Column = Supabase.Postgrest.Attributes.ColumnAttribute;
    using ClinicApp.Helpers;

    namespace ClinicApp.Models
    {
        [Table("bills")]
        public class SupabaseBill : BaseModel
        {
            [PrimaryKey("id")]
            public string Id { get; set; } = string.Empty;

            [Column("patient_id")]
            public string PatientId { get; set; } = string.Empty;

            [Column("patient_name")]
            public string PatientName { get; set; } = string.Empty;

            [Column("appointment_entry_id")]
            public string? AppointmentEntryId { get; set; }

            [Column("total_amount")]
            public decimal TotalAmount { get; set; }

            [Column("amount_paid")]
            public decimal AmountPaid { get; set; }

            [Column("balance")]
            public decimal Balance { get; set; }

            [Column("status")]
            public string Status { get; set; } = "unpaid";

            [Column("is_installment")]
            public bool IsInstallment { get; set; }

            [Column("installment_notes")]
            public string? InstallmentNotes { get; set; }

            [Column("due_date")]
            public DateTime? DueDate { get; set; }

            [Column("bill_number")]
            public string? BillNumber { get; set; }

            [Column("visit_date")]
            public DateTime VisitDate { get; set; }

            [Column("notes")]
            public string? Notes { get; set; }

            [Column("created_at")]
            public DateTime CreatedAt { get; set; }

            [Column("subtotal")]
            public decimal Subtotal { get; set; }

            [Column("discount_percent")]
            public decimal DiscountPercent { get; set; }

            [Column("discount_amount")]
            public decimal DiscountAmount { get; set; }

            [Column("installment_months")]
            public int InstallmentMonths { get; set; }

            [Column("monthly_payment")]
            public decimal MonthlyPayment { get; set; }

            [Column("last_payment_date")]
            public DateTime? LastPaymentDate { get; set; }

            [Ignore]
            [JsonIgnore]
            public string LastPaymentDateDisplay =>
        LastPaymentDate.HasValue
            ? LastPaymentDate.Value.ToLocalSafe().ToString("MMM dd, yyyy")
            : "—";

            [Ignore]
            [JsonIgnore]
            public string DueDateDisplay =>
                DueDate.HasValue
                    ? DueDate.Value.ToLocalSafe().ToString("MMM dd, yyyy")
                    : "—";

            [Ignore]
            [JsonIgnore]
            public bool HasDueDate => DueDate.HasValue;

            [Ignore]
            [JsonIgnore]
            public string InstallmentDisplay =>
                IsInstallment && InstallmentMonths > 0
                    ? $"{InstallmentMonths} months @ ₱{MonthlyPayment:N2}/month"
                    : string.Empty;

            // Display helpers — no [Column] needed
            [Ignore]
            [JsonIgnore]
            public string StatusDisplay => Status switch
            {
                "paid" => "Paid",
                "partial" => "Partial",
                "unpaid" => "Unpaid",
                _ => Status
            };

            [Ignore]
            [JsonIgnore]
            public Color StatusColor => Status switch
            {
                "paid" => Color.FromArgb("#2E7D32"),
                "partial" => Color.FromArgb("#E65100"),
                "unpaid" => Color.FromArgb("#C62828"),
                _ => Color.FromArgb("#888888")
            };

            [Ignore]
            [JsonIgnore]
            public Color StatusBgColor => Status switch
            {
                "paid" => Color.FromArgb("#E8F5E9"),
                "partial" => Color.FromArgb("#FFF3E0"),
                "unpaid" => Color.FromArgb("#FCEAEA"),
                _ => Color.FromArgb("#F5F5F5")
            };

            [Ignore]
            [JsonIgnore]
            public string TotalDisplay => $"₱{TotalAmount:N2}";

            [Ignore]
            [JsonIgnore]
            public string PaidDisplay => $"₱{AmountPaid:N2}";

            [Ignore]
            [JsonIgnore]
            public string BalanceDisplay => $"₱{Balance:N2}";
            [Ignore]
            [JsonIgnore]
            public string BillNumberDisplay => BillNumber ?? "—";
            [Ignore]
            [JsonIgnore]
            public string SubtotalDisplay => $"₱{Subtotal:N2}";

            [Ignore]
            [JsonIgnore]
            public string DiscountAmountDisplay => $"₱{DiscountAmount:N2}";

            [Ignore]
            [JsonIgnore]
            public string DiscountPercentDisplay =>
                DiscountPercent <= 0 ? "0%" : $"{DiscountPercent * 100m:N0}%";
            [Ignore]
            [JsonIgnore]
            public string DateDisplay =>
        VisitDate == default
            ? CreatedAt.ToLocalSafe().ToString("MMM dd, yyyy")
            : VisitDate.ToLocalSafe().ToString("MMM dd, yyyy");

            [Ignore]
            [JsonIgnore]
            public bool IsOverdue =>
        IsInstallment &&
        Balance > 0 &&
        DueDate.HasValue &&
        DateTime.Now.Date > DueDate.Value.ToLocalSafe().Date;

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
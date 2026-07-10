using SQLite;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Table = Supabase.Postgrest.Attributes.TableAttribute;
using PrimaryKey = Supabase.Postgrest.Attributes.PrimaryKeyAttribute;
using Column = Supabase.Postgrest.Attributes.ColumnAttribute;

namespace ClinicApp.Models
{

    [Table("transactions")]
    public class SupabaseTransaction : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = string.Empty;

        [Column("patient_id")]
        public string PatientId { get; set; } = string.Empty;

        [Column("treatment_record_id")]
        public string? TreatmentRecordId { get; set; }

        [Column("service_name")]
        public string ServiceName { get; set; } = string.Empty;

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("amount_paid")]
        public decimal AmountPaid { get; set; }

        [Column("balance")]
        public decimal Balance { get; set; }

        [Column("payment_status")]
        public string PaymentStatus { get; set; } = "unpaid";

        [Column("payment_date")]
        public DateTime? PaymentDate { get; set; }

        [Column("recorded_by")]
        public string? RecordedBy { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // Computed display helpers
        [Ignore]
        public string StatusLabel => PaymentStatus switch
        {
            "paid" => "Paid",
            "partial" => "Partial",
            "unpaid" => "Unpaid",
            _ => PaymentStatus
        };

        [Ignore]
        public Color StatusColor => PaymentStatus switch
        {
            "paid" => Color.FromArgb("#2E7D32"),
            "partial" => Color.FromArgb("#E65100"),
            "unpaid" => Color.FromArgb("#C62828"),
            _ => Color.FromArgb("#888780")
        };

        [Ignore]
        public Color StatusBgColor => PaymentStatus switch
        {
            "paid" => Color.FromArgb("#E8F5E9"),
            "partial" => Color.FromArgb("#FFF3E0"),
            "unpaid" => Color.FromArgb("#FCEAEA"),
            _ => Color.FromArgb("#F5F5F5")
        };

        [Ignore]
        public string VisitDateDisplay =>
            CreatedAt.ToLocalTime().ToString("MMM dd, yyyy");

        [Ignore]
        public string TotalDisplay => $"₱{TotalAmount:N2}";

        [Ignore]
        public string PaidDisplay => $"₱{AmountPaid:N2}";

        [Ignore]
        public string BalanceDisplay => $"₱{Balance:N2}";
    }
}
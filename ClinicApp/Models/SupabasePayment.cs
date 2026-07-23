using Newtonsoft.Json;
using SQLite;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Table = Supabase.Postgrest.Attributes.TableAttribute;
using PrimaryKey = Supabase.Postgrest.Attributes.PrimaryKeyAttribute;
using Column = Supabase.Postgrest.Attributes.ColumnAttribute;
using ClinicApp.Helpers;

namespace ClinicApp.Models
{
    [Table("payments")]
    public class SupabasePayment : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = string.Empty;

        [Column("bill_id")]
        public string BillId { get; set; } = string.Empty;

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("payment_date")]
        public DateTime PaymentDate { get; set; }

        [Column("recorded_by")]
        public string? RecordedBy { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Ignore]
        [JsonIgnore]
        public string AmountDisplay =>
            $"₱{Amount:N2}";

        [Ignore]
        [JsonIgnore]
    public string DateDisplay =>
    PaymentDate.ToLocalSafe().ToString("MMM dd, yyyy h:mm tt");
    }
}
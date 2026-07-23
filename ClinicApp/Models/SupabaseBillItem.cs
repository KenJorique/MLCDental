using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models
{
    [Table("bill_items")]
    public class SupabaseBillItem : BaseModel
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

        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("tooth_numbers")]
        public string? ToothNumbers { get; set; }

        [Column("affects_teeth")]
        public bool AffectsTeeth { get; set; }

        // Display helpers
        public string ToothNumbersDisplay =>
            string.IsNullOrEmpty(ToothNumbers)
                ? "" : $"Teeth: {ToothNumbers}";

        public bool HasTeethInfo =>
            !string.IsNullOrEmpty(ToothNumbers);

        public string SubtotalDisplay => $"₱{Subtotal:N2}";
        public string UnitPriceDisplay => $"₱{UnitPrice:N2}";
    }
}
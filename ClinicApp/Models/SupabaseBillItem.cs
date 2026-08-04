using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;
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

        [Column("subtotal")]
        [JsonProperty("subtotal")]
        public decimal Subtotal { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("tooth_numbers")]
        public string? ToothNumbers { get; set; }

        [Column("affects_teeth")]
        public bool AffectsTeeth { get; set; }


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

    }
}
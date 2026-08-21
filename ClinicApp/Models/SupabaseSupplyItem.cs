using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models
{
    [Table("supplies")]
    public class SupabaseSupplyItem : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("unit")]
        public string Unit { get; set; } = "Per Piece";

        [Column("pieces_per_unit")]
        public int PiecesPerUnit { get; set; } = 1;

        [Column("quantity_in_pieces")]
        public int QuantityInPieces { get; set; } = 0;

        [Column("has_expiration")]
        public bool HasExpiration { get; set; } = false;

        [Column("expiration_date")]
        public DateTime? ExpirationDate { get; set; }

        [Column("minimum_stock_pieces")]
        public int MinimumStockPieces { get; set; } = 10;

        [Column("added_date")]
        public DateTime? AddedDate { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // Auto-maintained by a Postgres trigger (supplies_set_updated_at) —
        // set on every UPDATE, so no C# code needs to touch this manually.
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonIgnore]
        public bool IsLowStock => QuantityInPieces <= MinimumStockPieces;

        [JsonIgnore]
        public bool IsOutOfStock => QuantityInPieces <= 0;

        [JsonIgnore]
        public string QuantityDisplay => $"{QuantityInPieces} pcs";

        [JsonIgnore]
        public string ExpirationDateDisplay => ExpirationDate?.ToString("yyyy-MM-dd") ?? string.Empty;
    }
}
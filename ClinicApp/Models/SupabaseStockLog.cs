using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models
{
    [Table("supply_stock_logs")]
    public class SupabaseStockLog : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("supply_id")]
        public string SupplyId { get; set; } = string.Empty;

        [Column("change_type")]
        public string ChangeType { get; set; } = string.Empty;

        [Column("change_in_pieces")]
        public int ChangeInPieces { get; set; }

        [Column("stock_after_change")]
        public int StockAfterChange { get; set; }

        [Column("patient_id")]
        public string? PatientId { get; set; }

        [Column("patient_name")]
        public string? PatientName { get; set; }

        [Column("note")]
        public string? Note { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
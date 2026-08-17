using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models
{
    [Table("service_supplies")]
    public class SupabaseServiceSupply : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("service_id")]
        public string ServiceId { get; set; } = string.Empty;

        [Column("supply_id")]
        public string SupplyId { get; set; } = string.Empty;

        [Column("quantity_used")]
        public decimal QuantityUsed { get; set; } = 1;
    }
}
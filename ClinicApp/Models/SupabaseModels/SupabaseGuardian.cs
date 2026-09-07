using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models.SupabaseModels
{
    [Table("guardians")]
    public class SupabaseGuardian : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("occupation")]
        public string? Occupation { get; set; }

        [Column("mobile")]
        public string? Mobile { get; set; }
    }
}

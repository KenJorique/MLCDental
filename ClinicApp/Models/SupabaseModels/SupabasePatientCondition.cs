using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models.SupabaseModels
{
    [Table("patient_conditions")]
    public class SupabasePatientCondition : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("patient_id")]
        public string PatientId { get; set; } = string.Empty;

        [Column("condition_id")]
        public long ConditionId { get; set; }
    }
}

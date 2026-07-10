using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models
{
    [Table("treatment_records")]
    public class SupabaseTreatmentRecord : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = string.Empty;

        [Column("patient_id")]
        public string PatientId { get; set; } = string.Empty;

        [Column("service_name")]
        public string ServiceName { get; set; } = string.Empty;

        [Column("service_price")]
        public decimal ServicePrice { get; set; }

        [Column("visit_date")]
        public DateTime VisitDate { get; set; } = DateTime.UtcNow;

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("recorded_by")]
        public string? RecordedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models.SupabaseModels
{
    [Table("cancelled_appointments")]
    // One row per cancellation — the only trace left behind once the real appointment_entries row is hard-deleted, so Reports has something to count.
    public class SupabaseCancelledAppointment : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("appointment_datetime")]
        public DateTime AppointmentDateTime { get; set; } 

        [Column("patient_name")]
        public string? PatientName { get; set; } 

        [Column("cancelled_at")]
        public DateTime CancelledAt { get; set; }
    }
}

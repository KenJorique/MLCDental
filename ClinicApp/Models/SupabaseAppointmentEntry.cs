using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models
{
    [Table("appointment_entries")]
    public class SupabaseAppointmentEntry : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = string.Empty;

        [Column("supabase_booking_id")]
        public string SupabaseBookingId { get; set; } = string.Empty;

        [Column("patient_name")]
        public string PatientName { get; set; } = string.Empty;

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("service")]
        public string? Service { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("appointment_datetime")]
        public DateTime AppointmentDateTime { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("google_task_id")]
        public string? GoogleTaskId { get; set; }
    }
}
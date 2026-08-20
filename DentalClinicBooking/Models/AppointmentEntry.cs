using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace DentalClinicBooking.Models
{
    // Read-only mirror of the mobile app's appointment_entries table
    // (see ClinicApp.Models.SupabaseAppointmentEntry). A row here only
    // exists once staff approve a booking (or create a walk-in) —
    // NOT the moment a patient submits the public form. That makes this
    // table, not `bookings`, the correct source of truth for "is this
    // slot actually taken": a pending website submission should never
    // block the slot for other patients, only an approved one should.
  
    [Table("appointment_entries")]
    public class AppointmentEntry : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = string.Empty;

        [Column("appointment_datetime")]
        public DateTime AppointmentDateTime { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending";
    }
}

using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace DentalClinicBooking.Models
{
    [Table("bookings")]
    public class Booking : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("patient_id")]
        public string PatientId { get; set; } = string.Empty;

        [Column("appointment_date")]
        public DateTime AppointmentDate { get; set; }

        [Column("service")]
        public string? Service { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }
    }
}
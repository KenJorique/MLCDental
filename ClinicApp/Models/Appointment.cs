using SQLite;

namespace ClinicApp.Models
{
    [Table("Appointment")]
    public class Appointment
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string SupabaseBookingId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string AppointmentDate { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // pending/approved/rejected
        public string ReceivedAt { get; set; } = string.Empty;
    }
}
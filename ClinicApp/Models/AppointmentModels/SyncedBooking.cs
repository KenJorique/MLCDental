using SQLite;

namespace ClinicApp.Models.AppointmentModels
{
    [Table("SyncedBooking")]
    public class SyncedBooking
    {
        [PrimaryKey]
        public string SupabaseId { get; set; } = string.Empty;
        public DateTime SyncedAt { get; set; }
    }
}
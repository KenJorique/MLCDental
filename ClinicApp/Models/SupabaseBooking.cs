
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models
{
    [Table("bookings")]
    public class SupabaseBooking : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = string.Empty;

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("email")]
        public string? Email { get; set; }

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

        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        [Column("is_existing_patient")]
        public bool IsExistingPatient { get; set; } = false;

        [Column("existing_patient_id")]
        public string? ExistingPatientId { get; set; }
        //public string AppointmentDateDisplay
        //{
        //    get
        //    {
        //        var utc = AppointmentDate.Kind == DateTimeKind.Utc
        //            ? AppointmentDate
        //            : DateTime.SpecifyKind(AppointmentDate, DateTimeKind.Utc);
        //        return utc.ToLocalTime().ToString("MMM dd, yyyy  h:mm tt");
        //    }
        //}

    }


}
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace DentalClinicBooking.Models
{
    [Table("patients")]
    public class Patient : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
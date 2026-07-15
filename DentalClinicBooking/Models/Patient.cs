using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace DentalClinicBooking.Models
{
    [Table("patients")]
    public class Patient : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        // Computed
        public string FullName =>
            $"{FirstName} {LastName}".Trim();
    }
}
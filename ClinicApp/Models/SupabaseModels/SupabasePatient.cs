using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models.SupabaseModels
{
    [Table("patients")]
    public class SupabasePatient : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = string.Empty;

        // Personal — matches your actual columns
        [Column("first_name")] public string FirstName { get; set; } = string.Empty;
        [Column("last_name")] public string? LastName { get; set; }
        [Column("nickname")] public string? Nickname { get; set; }
        [Column("gender")] public string? Gender { get; set; }
        [Column("date_of_birth")] public DateTime? DateOfBirth { get; set; }
        [Column("nationality")] public string? Nationality { get; set; }
        [Column("religion")] public string? Religion { get; set; }
        [Column("occupation")] public string? Occupation { get; set; }
        [Column("address")] public string? Address { get; set; }

        [Column("date_registered")]
        public DateTime DateRegistered { get; set; } = DateTime.UtcNow;

        // Contact
        [Column("phone")] public string? Phone { get; set; }
        [Column("home_no")] public string? HomeNo { get; set; }
        [Column("office_no")] public string? OfficeNo { get; set; }
        [Column("fax_no")] public string? FaxNo { get; set; }
        [Column("email")] public string? Email { get; set; }

        // Referral
        [Column("referred_by")] public string? ReferredBy { get; set; }

        // Guardian
        [Column("guardian_name")] public string? GuardianName { get; set; }
        [Column("guardian_relationship")] public string? GuardianRelationship { get; set; }
        [Column("guardian_occupation")] public string? GuardianOccupation { get; set; }
        [Column("guardian_mobile")] public string? GuardianMobile { get; set; }

        // Medical History (Health Status)
        [Column("blood_type")] public string? BloodType { get; set; }
        [Column("good_health")] public bool GoodHealth { get; set; }
        [Column("under_treatment")] public bool UnderTreatment { get; set; }
        [Column("hospitalized")] public bool Hospitalized { get; set; }
        [Column("uses_tobacco")] public bool UsesTobacco { get; set; }
        [Column("on_medications")] public bool OnMedications { get; set; }

        // Allergies
        [Column("latex_allergy")] public bool LatexAllergy { get; set; }
        [Column("aspirin_allergy")] public bool AspirinAllergy { get; set; }
        [Column("penicillin_allergy")] public bool PenicillinAllergy { get; set; }
        [Column("sulfa_allergy")] public bool SulfaAllergy { get; set; }
        [Column("local_anesthetic_allergy")] public bool LocalAnestheticAllergy { get; set; }
        [Column("other_allergy")] public string? OtherAllergy { get; set; }

        // Conditions as comma-separated string
        [Column("conditions")] public string? Conditions { get; set; }
    }
}
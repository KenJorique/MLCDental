using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models
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
         
        // Referral & Insurance
        [Column("referred_by")] public string? ReferredBy { get; set; }
        [Column("reason_for_consultation")] public string? ReasonForConsultation { get; set; }
        [Column("dental_insurance")] public string? DentalInsurance { get; set; }
        [Column("insurance_effective_date")] public DateTime? InsuranceEffectiveDate { get; set; }
        [Column("has_insurance")] public bool HasInsurance { get; set; }
        [Column("has_dental_insurance")] public bool HasDentalInsurance { get; set; }

        // Guardian
        [Column("guardian_name")] public string? GuardianName { get; set; }
        [Column("guardian_relationship")] public string? GuardianRelationship { get; set; }
        [Column("guardian_occupation")] public string? GuardianOccupation { get; set; }
        [Column("guardian_mobile")] public string? GuardianMobile { get; set; }

        // Medical History
        [Column("blood_type")] public string? BloodType { get; set; }
        [Column("blood_pressure")] public string? BloodPressure { get; set; }
        [Column("bleeding_time")] public string? BleedingTime { get; set; }
        [Column("physician_name")] public string? PhysicianName { get; set; }
        [Column("good_health")] public bool GoodHealth { get; set; }
        [Column("pregnant")] public bool Pregnant { get; set; }
        [Column("under_treatment")] public bool UnderTreatment { get; set; }
        [Column("medication_details")] public string? MedicationDetails { get; set; }
        [Column("hospitalized")] public bool Hospitalized { get; set; }
        [Column("hospitalization_details")] public string? HospitalizationDetails { get; set; }
        [Column("uses_tobacco")] public bool UsesTobacco { get; set; }
        [Column("uses_alcohol")] public bool UsesAlcohol { get; set; }
        [Column("on_medications")] public bool OnMedications { get; set; }
        [Column("taking_medications")] public bool TakingMedications { get; set; }
        [Column("previous_dentist")] public string? PreviousDentist { get; set; }
        [Column("last_dental_visit")] public string? LastDentalVisit { get; set; }

        // Allergies
        [Column("latex_allergy")] public bool LatexAllergy { get; set; }
        [Column("aspirin_allergy")] public bool AspirinAllergy { get; set; }
        [Column("penicillin_allergy")] public bool PenicillinAllergy { get; set; }
        [Column("sulfa_allergy")] public bool SulfaAllergy { get; set; }
        [Column("local_anesthetic_allergy")] public bool LocalAnestheticAllergy { get; set; }
        [Column("other_allergy")] public string? OtherAllergy { get; set; }

        // Conditions as comma-separated string
        [Column("conditions")] public string? Conditions { get; set; }

        // Links to local SQLite PatientID
        [Column("supabase_id")] public string? SupabaseId { get; set; }

        
    }
}
using SQLite;

namespace ClinicApp.Models;

[Table("Patient")]
public class Patient
{
    [PrimaryKey, AutoIncrement]
    public int PatientID { get; set; }

    // ── Basic Info ───────────────────────────────────────────────
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;

    // Gender stored as string ("Male"/"Female"/"Other")
    public string Gender { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Religion { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;

    // ── Contact ──────────────────────────────────────────────────
    public string MobileNo { get; set; } = string.Empty;
    public string HomeNo { get; set; } = string.Empty;
    public string FaxNo { get; set; } = string.Empty;
    public string OfficeNo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // ── Referral / Insurance ─────────────────────────────────────
    public string ReferredBy { get; set; } = string.Empty;
    public string ReasonForConsultation { get; set; } = string.Empty;
    public string DentalInsurance { get; set; } = string.Empty;
    public string InsuranceEffectiveDate { get; set; } = string.Empty;

    // ── Registration ─────────────────────────────────────────────
    public string DateRegistered { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

    // ── Last updated timestamp for Personal Info tab ──────────────
    // Saved every time the user taps Save Changes on the Personal Info tab
    public string LastUpdated { get; set; } = string.Empty;

    // ── Legacy fields (kept for backward compat) ─────────────────
    public string MedicalHistory { get; set; } = string.Empty;
    public bool HasNoMedicalHistory { get; set; } = false;

    // supabase_id is the UUID from Supabase, used to link local patient record with remote one
    public string SupabaseId { get; set; } = string.Empty;

    // ── Computed (not stored) ─────────────────────────────────────
    [Ignore]
    public string FullName => $"{FirstName} {LastName}".Trim();

    [Ignore]
    public int Age
    {
        get
        {
            if (!DateTime.TryParse(DateOfBirth, out var dob)) return 0;
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}

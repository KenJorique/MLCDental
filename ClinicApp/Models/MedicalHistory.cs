using SQLite;

namespace ClinicApp.Models;

[Table("MedicalHistory")]
public class MedicalHistory
{
    [PrimaryKey, AutoIncrement]
    public int MedicalHistoryID { get; set; }

    [Indexed]
    public int PatientID { get; set; }   // FK → Patient

    // General
    public string BloodType { get; set; } = string.Empty;
    public string PhysicianName { get; set; } = string.Empty;
    public bool IsGoodHealth { get; set; } = true;
    public bool IsPregnant { get; set; } = false;
    public bool UnderMedicalTreatment { get; set; } = false;
    public string MedicationDetails { get; set; } = string.Empty;
    public bool HasBeenHospitalized { get; set; } = false;
    public string HospitalizationDetails { get; set; } = string.Empty;
    public bool HasAllergy { get; set; } = false;
    public string BleedingTime { get; set; } = string.Empty;
    public string BloodPressure { get; set; } = string.Empty;

    // Habits
    public bool UsesTobacco { get; set; } = false;
    public bool UsesAlcohol { get; set; } = false;
    public bool TakingMedications { get; set; } = false;

    // Previous dental
    public string PreviousDentist { get; set; } = string.Empty;
    public string LastDentalVisit { get; set; } = string.Empty;

    public string LastUpdated { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
}

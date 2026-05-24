using SQLite;

namespace ClinicApp.Models;

/// <summary>
/// Many-to-many link between Patient and MedicalCondition.
/// </summary>
[Table("PatientCondition")]
public class PatientCondition
{
    [PrimaryKey, AutoIncrement]
    public int PatientConditionID { get; set; }

    [Indexed]
    public int PatientID { get; set; }   // FK → Patient
    public int ConditionID { get; set; }   // FK → MedicalCondition
}

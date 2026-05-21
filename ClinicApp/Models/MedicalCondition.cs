using SQLite;

namespace ClinicApp.Models;

/// <summary>
/// Lookup table — list of possible medical conditions (e.g. Diabetes, Hypertension).
/// </summary>
[Table("MedicalCondition")]
public class MedicalCondition
{
    [PrimaryKey, AutoIncrement]
    public int ConditionID { get; set; }
    public string ConditionName { get; set; } = string.Empty;
}

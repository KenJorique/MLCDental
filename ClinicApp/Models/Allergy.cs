using SQLite;

namespace ClinicApp.Models;

[Table("Allergy")]
public class Allergy
{
    [PrimaryKey, AutoIncrement]
    public int AllergyID { get; set; }

    [Indexed]
    public int PatientID { get; set; }   // FK → Patient

    public bool HasLatexAllergy { get; set; } = false;
    public bool HasAspirinAllergy { get; set; } = false;
    public bool HasPenicillinAllergy { get; set; } = false;
    public bool HasSulfaAllergy { get; set; } = false;
    public bool HasLocalAnestheticAllergy { get; set; } = false;
    public string OtherAllergy { get; set; } = string.Empty;
}

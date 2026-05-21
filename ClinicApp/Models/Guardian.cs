using SQLite;

namespace ClinicApp.Models;

[Table("Guardian")]
public class Guardian
{
    [PrimaryKey, AutoIncrement]
    public int GuardianID { get; set; }

    [Indexed]
    public int PatientID { get; set; }   // FK → Patient

    public string GuardianName { get; set; } = string.Empty;
    public string RelationshipToPatient { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
}

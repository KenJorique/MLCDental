using SQLite;

namespace ClinicApp.Models.TreatmentModels;

[Table("TreatmentHistory")]
public class TreatmentHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int PatientId { get; set; }

    public int ToothNumber { get; set; }

    public string ToothName { get; set; } = string.Empty;

    public string Condition { get; set; } = string.Empty;

    public string PreviousCondition { get; set; } = string.Empty;

    public string Color { get; set; } = "#FFFFFF";

    public string Notes { get; set; } = string.Empty;

    public string ActionType { get; set; } = "Added";

    public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public string Description { get; set; } = string.Empty;

    /// <summary>Row id in the Supabase "treatment_history" table, used to prevent duplicate sync.</summary>
    public string SupabaseId { get; set; } = string.Empty;
}

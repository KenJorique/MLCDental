using SQLite;

namespace ClinicApp.Models;

[Table("TreatmentHistory")]
public class TreatmentHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int PatientId { get; set; }

    /// <summary>
    /// Which tooth was treated (1–32, Universal Numbering System).
    /// </summary>
    public int ToothNumber { get; set; }

    /// <summary>
    /// Tooth name, e.g. "UR6 · 1st Molar"
    /// </summary>
    public string ToothName { get; set; } = string.Empty;

    /// <summary>
    /// The condition/treatment applied, e.g. "Filling", "Caries", "Completed", "Missing"
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Previous condition before this change (empty if it's the first record)
    /// </summary>
    public string PreviousCondition { get; set; } = string.Empty;

    /// <summary>Hex color string e.g. "#FF0000"</summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>
    /// Clinical notes recorded at the time of treatment.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// "Added" for new records, "Updated" for edits, "Cleared" for resets
    /// </summary>
    public string ActionType { get; set; } = "Added";

    /// <summary>Full ISO timestamp of when the record was created.</summary>
    public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public string Description { get; set; } = string.Empty;
}

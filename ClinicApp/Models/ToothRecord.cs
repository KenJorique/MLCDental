using SQLite;

namespace ClinicApp.Models;

[Table("ToothRecords")]
public class ToothRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int PatientId { get; set; }

    /// <summary>
    /// Universal Numbering System 1–32.
    /// Upper arch: 1–16 (right to left). Lower arch: 17–32 (left to right).
    /// </summary>
    public int ToothNumber { get; set; }

    /// <summary>
    /// "Normal" | "Filling" | "Caries" | "Completed" | "Missing"
    /// </summary>
    public string Condition { get; set; } = "Normal";

    /// <summary>Hex color string e.g. "#FF0000"</summary>
    public string Color { get; set; } = "#FFFFFF";

    public string Notes { get; set; } = string.Empty;

    [Column("LastUpdated")]
    public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");

    [Column("DateUpdated")]
    public string DateUpdated { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
}

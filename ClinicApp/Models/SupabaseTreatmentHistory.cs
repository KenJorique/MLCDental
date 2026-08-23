using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models;

[Table("treatment_history")]
public class SupabaseTreatmentHistory : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("patient_id")]
    public string PatientId { get; set; } = string.Empty; // Supabase patients.id

    [Column("tooth_number")]
    public int ToothNumber { get; set; }

    [Column("tooth_name")]
    public string ToothName { get; set; } = string.Empty;

    [Column("condition")]
    public string Condition { get; set; } = string.Empty;

    [Column("previous_condition")]
    public string PreviousCondition { get; set; } = string.Empty;

    [Column("color")]
    public string Color { get; set; } = "#FFFFFF";

    [Column("notes")]
    public string Notes { get; set; } = string.Empty;

    [Column("action_type")]
    public string ActionType { get; set; } = "Added";

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
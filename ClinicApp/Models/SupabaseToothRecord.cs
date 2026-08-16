// File: Models/SupabaseToothRecord.cs
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models;

[Table("tooth_records")]
public class SupabaseToothRecord : BaseModel
{
    [PrimaryKey("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [Column("patient_id")]
    [JsonProperty("patient_id")]
    public string PatientId { get; set; } = string.Empty;

    [Column("tooth_number")]
    [JsonProperty("tooth_number")]
    public int ToothNumber { get; set; }

    [Column("condition")]
    [JsonProperty("condition")]
    public string Condition { get; set; } = "Normal";

    [Column("color")]
    [JsonProperty("color")]
    public string Color { get; set; } = "#FFFFFF";

    [Column("notes")]
    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;

    [Column("last_updated")]
    [JsonProperty("last_updated")]
    public string LastUpdated { get; set; } = string.Empty;
}
using SQLite;

namespace ClinicApp.Models;

public class CephalometricMeasurement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int PatientId { get; set; }
    public DateTime MeasurementDate { get; set; }

    // Linear measurements (mm)
    public double? SNA_Angle { get; set; }      // Sella-Nasion-A point
    public double? SNB_Angle { get; set; }      // Sella-Nasion-B point
    public double? ANB_Angle { get; set; }      // A point-Nasion-B point (ANB = SNA - SNB)
    public double? SN_GoGn { get; set; }        // SN plane to Go-Gn (mandibular plane)
    public double? FMA { get; set; }            // Frankfort-Mandibular plane angle
    public double? IMPA { get; set; }           // Incisor-Mandibular plane angle
    public double? U1_SN { get; set; }          // Upper incisor to SN plane
    public double? L1_MP { get; set; }          // Lower incisor to mandibular plane

    // Notes
    public string? Notes { get; set; }

    // JSON string of all landmarks (backup)
    public string? LandmarkData { get; set; }
}

public class MeasurementResult
{
    public string MeasurementName { get; set; } = "";
    public double Value { get; set; }
    public double NormalMin { get; set; }
    public double NormalMax { get; set; }
    public string Unit { get; set; } = "°";

    public string Status
    {
        get
        {
            if (Value < NormalMin) return "Low";
            if (Value > NormalMax) return "High";
            return "Normal";
        }
    }

    public Color StatusColor
    {
        get
        {
            return Status switch
            {
                "Normal" => Colors.Green,
                "High" => Colors.Orange,
                "Low" => Colors.Blue,
                _ => Colors.Gray
            };
        }
    }
}
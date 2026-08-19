namespace ClinicApp.Config;

/// <summary>
/// Cephalometric normal ranges by age group and ethnicity
/// Based on published orthodontic standards (Caucasian, adult)
/// </summary>
public static class NormalRanges
{
    public class MeasurementRange
    {
        public string Name { get; set; } = "";
        public double Min { get; set; }
        public double Max { get; set; }
        public string Unit { get; set; } = "°";
        public string Description { get; set; } = "";
    }

    // Adult normal ranges (Caucasian) - Most commonly used
    public static Dictionary<string, MeasurementRange> AdultNormal = new()
    {
        // Skeletal relationships
        ["SNA"] = new()
        {
            Name = "SNA Angle",
            Min = 80,
            Max = 84,
            Unit = "°",
            Description = "Sella-Nasion to A point"
        },
        ["SNB"] = new()
        {
            Name = "SNB Angle",
            Min = 77,
            Max = 82,
            Unit = "°",
            Description = "Sella-Nasion to B point"
        },
        ["ANB"] = new()
        {
            Name = "ANB Angle",
            Min = 2,
            Max = 4,
            Unit = "°",
            Description = "Difference between SNA and SNB"
        },

        // Vertical dimensions
        ["FMA"] = new()
        {
            Name = "FMA",
            Min = 21,
            Max = 29,
            Unit = "°",
            Description = "Frankfort-Mandibular plane angle"
        },
        ["SN_GoGn"] = new()
        {
            Name = "SN-GoGn",
            Min = 28,
            Max = 35,
            Unit = "°",
            Description = "SN plane to Go-Gn plane"
        },

        // Incisor angles
        ["U1_SN"] = new()
        {
            Name = "U1 to SN",
            Min = 100,
            Max = 110,
            Unit = "°",
            Description = "Upper incisor to SN plane"
        },
        ["L1_MP"] = new()
        {
            Name = "L1 to MP",
            Min = 85,
            Max = 95,
            Unit = "°",
            Description = "Lower incisor to mandibular plane"
        },
        ["IMPA"] = new()
        {
            Name = "IMPA",
            Min = 85,
            Max = 95,
            Unit = "°",
            Description = "Incisor-Mandibular plane angle"
        },

        // Horizontal relationships
        ["Co_Gn"] = new()
        {
            Name = "Co-Gn",
            Min = 110,
            Max = 125,
            Unit = "mm",
            Description = "Condyle to Gnathion distance"
        },
        ["Co_A"] = new()
        {
            Name = "Co-A",
            Min = 86,
            Max = 106,
            Unit = "mm",
            Description = "Condyle to A point distance"
        },

        // Facial heights
        ["AFH"] = new()
        {
            Name = "AFH",
            Min = 115,
            Max = 135,
            Unit = "mm",
            Description = "Anterior facial height (N-Me)"
        },
        ["PFH"] = new()
        {
            Name = "PFH",
            Min = 70,
            Max = 85,
            Unit = "mm",
            Description = "Posterior facial height (Po-Go)"
        },
    };

    /// <summary>
    /// Get normal range for a measurement
    /// </summary>
    public static MeasurementRange? GetRange(string measurementName)
    {
        if (AdultNormal.TryGetValue(measurementName, out var range))
            return range;
        return null;
    }

    /// <summary>
    /// Check if a value is within normal range
    /// </summary>
    public static string CheckStatus(string measurementName, double value)
    {
        var range = GetRange(measurementName);
        if (range == null) return "Unknown";

        if (value < range.Min) return "Low";
        if (value > range.Max) return "High";
        return "Normal";
    }

    /// <summary>
    /// Get all measurements as list for UI binding
    /// </summary>
    public static List<MeasurementRange> GetAllMeasurements()
    {
        return AdultNormal.Values.ToList();
    }
}
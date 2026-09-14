namespace ClinicApp.Services;

public static class CephalometricCalculations
{
    private const string SELLA = "Sella";
    private const string NASION = "Nasion";
    private const string ORBITALE = "Orbitale";
    private const string PORION = "Porion";
    private const string A_POINT = "Subspinale";
    private const string B_POINT = "Supramentale";
    private const string POGONION = "Pogonion";
    private const string MENTON = "Menton";
    private const string GNATHION = "Gnathion";
    private const string GONION = "Gonion";
    private const string LOWER_INCISOR = "Incision inferius";
    private const string UPPER_INCISOR = "Incision superius";
    private const string UPPER_LIP = "Upper lip";
    private const string LOWER_LIP = "Lower lip";
    private const string SUBNASALE = "Subnasale";
    private const string SOFT_POGONION = "Soft tissue pogonion";
    private const string PNS = "Posterior nasal spine";
    private const string ANS = "Anterior nasal spine";
    private const string ARTICULARE = "Articulare";

    public static Dictionary<string, double> CalculateMeasurements(List<Landmark> landmarks)
    {
        var results = new Dictionary<string, double>();
        if (landmarks.Count < 4) return results;

        var byName = landmarks
            .Where(l => !string.IsNullOrEmpty(l.ClassName))
            .GroupBy(l => l.ClassName!)
            .ToDictionary(g => g.Key, g => g.First());

        bool Has(params string[] names) => names.All(byName.ContainsKey);

        if (Has(SELLA, NASION, A_POINT))
            results["SNA"] = AngleBetween(byName[SELLA], byName[NASION], byName[A_POINT]);
        if (Has(SELLA, NASION, B_POINT))
            results["SNB"] = AngleBetween(byName[SELLA], byName[NASION], byName[B_POINT]);
        if (results.ContainsKey("SNA") && results.ContainsKey("SNB"))
            results["ANB"] = results["SNA"] - results["SNB"];
        if (Has(PORION, ORBITALE, GONION, MENTON))
            results["FMA"] = AngleBetween(byName[PORION], byName[ORBITALE], byName[GONION], byName[MENTON]);
        if (Has(SELLA, NASION, GONION, MENTON))
            results["SN_GoGn"] = AngleBetween(byName[SELLA], byName[NASION], byName[GONION], byName[MENTON]);
        if (Has(NASION, MENTON))
            results["AFH"] = Distance(byName[NASION], byName[MENTON]);
        if (Has(PORION, GONION))
            results["PFH"] = Distance(byName[PORION], byName[GONION]);

        return results;
    }

    private static double AngleBetween(Landmark p1, Landmark vertex, Landmark p2)
    {
        var v1 = new { x = p1.X - vertex.X, y = p1.Y - vertex.Y };
        var v2 = new { x = p2.X - vertex.X, y = p2.Y - vertex.Y };
        double dot = v1.x * v2.x + v1.y * v2.y;
        double mag1 = Math.Sqrt(v1.x * v1.x + v1.y * v1.y);
        double mag2 = Math.Sqrt(v2.x * v2.x + v2.y * v2.y);
        if (mag1 == 0 || mag2 == 0) return 0;
        double cosAngle = Math.Clamp(dot / (mag1 * mag2), -1, 1);
        return Math.Acos(cosAngle) * (180.0 / Math.PI);
    }

    private static double AngleBetween(Landmark p1, Landmark p2, Landmark p3, Landmark p4)
    {
        var v1 = new { x = p2.X - p1.X, y = p2.Y - p1.Y };
        var v2 = new { x = p4.X - p3.X, y = p4.Y - p3.Y };
        double dot = v1.x * v2.x + v1.y * v2.y;
        double mag1 = Math.Sqrt(v1.x * v1.x + v1.y * v1.y);
        double mag2 = Math.Sqrt(v2.x * v2.x + v2.y * v2.y);
        if (mag1 == 0 || mag2 == 0) return 0;
        double cosAngle = Math.Clamp(dot / (mag1 * mag2), -1, 1);
        return Math.Acos(cosAngle) * (180.0 / Math.PI);
    }

    private static double Distance(Landmark p1, Landmark p2)
    {
        double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
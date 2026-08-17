namespace ClinicApp.Services;

public static class CephalometricCalculations
{
    private const int SELLA = 0;
    private const int NASION = 1;
    private const int ORBITALE = 2;
    private const int PORION = 3;
    private const int A_POINT = 4;
    private const int B_POINT = 5;
    private const int POGONION = 6;
    private const int MENTON = 7;
    private const int GNATHION = 8;
    private const int GONION = 9;
    private const int LOWER_INCISOR = 10;
    private const int UPPER_INCISOR = 11;
    private const int UPPER_LIP = 12;
    private const int LOWER_LIP = 13;
    private const int SUBNASALE = 14;
    private const int SOFT_POGONION = 15;
    private const int PNS = 16;
    private const int ANS = 17;
    private const int ARTICULARE = 18;

    public static Dictionary<string, double> CalculateMeasurements(List<Landmark> landmarks)
    {
        var results = new Dictionary<string, double>();

        System.Diagnostics.Debug.WriteLine($"📊 Calculating from {landmarks.Count} landmarks");

        if (landmarks.Count < 4)
        {
            System.Diagnostics.Debug.WriteLine("❌ Need at least 4 landmarks");
            return results;
        }

        try
        {
            var pts = landmarks.ToArray();

            // Build a map of available landmarks by ClassId
            var availableById = landmarks.ToDictionary(l => l.ClassId, l => l);

            // Only calculate if we have the required points for each measurement

            // SNA: Sella (0), Nasion (1), A-point (4)
            if (availableById.ContainsKey(SELLA) && availableById.ContainsKey(NASION) && availableById.ContainsKey(A_POINT))
            {
                results["SNA"] = AngleBetween(availableById[SELLA], availableById[NASION], availableById[A_POINT]);
                System.Diagnostics.Debug.WriteLine($"✅ SNA: {results["SNA"]:F2}");
            }

            // SNB: Sella (0), Nasion (1), B-point (5)
            if (availableById.ContainsKey(SELLA) && availableById.ContainsKey(NASION) && availableById.ContainsKey(B_POINT))
            {
                results["SNB"] = AngleBetween(availableById[SELLA], availableById[NASION], availableById[B_POINT]);
                System.Diagnostics.Debug.WriteLine($"✅ SNB: {results["SNB"]:F2}");
            }

            // ANB: difference between SNA and SNB
            if (results.ContainsKey("SNA") && results.ContainsKey("SNB"))
            {
                results["ANB"] = results["SNA"] - results["SNB"];
                System.Diagnostics.Debug.WriteLine($"✅ ANB: {results["ANB"]:F2}");
            }

            // FMA: Frankfort plane (Porion-Orbitale) to Mandibular plane (Gonion-Menton)
            if (availableById.ContainsKey(PORION) && availableById.ContainsKey(ORBITALE)
                && availableById.ContainsKey(GONION) && availableById.ContainsKey(MENTON))
            {
                results["FMA"] = AngleBetween(availableById[PORION], availableById[ORBITALE],
                                            availableById[GONION], availableById[MENTON]);
                System.Diagnostics.Debug.WriteLine($"✅ FMA: {results["FMA"]:F2}");
            }

            // SN-GoGn: SN plane to Go-Gn plane
            if (availableById.ContainsKey(SELLA) && availableById.ContainsKey(NASION)
                && availableById.ContainsKey(GONION) && availableById.ContainsKey(MENTON))
            {
                results["SN_GoGn"] = AngleBetween(availableById[SELLA], availableById[NASION],
                                                 availableById[GONION], availableById[MENTON]);
                System.Diagnostics.Debug.WriteLine($"✅ SN_GoGn: {results["SN_GoGn"]:F2}");
            }

            // Vertical heights
            if (availableById.ContainsKey(NASION) && availableById.ContainsKey(MENTON))
            {
                results["AFH"] = Distance(availableById[NASION], availableById[MENTON]);
                System.Diagnostics.Debug.WriteLine($"✅ AFH: {results["AFH"]:F2}mm");
            }

            if (availableById.ContainsKey(PORION) && availableById.ContainsKey(GONION))
            {
                results["PFH"] = Distance(availableById[PORION], availableById[GONION]);
                System.Diagnostics.Debug.WriteLine($"✅ PFH: {results["PFH"]:F2}mm");
            }

            System.Diagnostics.Debug.WriteLine($"✅ Total measurements calculated: {results.Count}");

            return results;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Calculation error: {ex.Message}");
            return results;
        }
    }

    private static double AngleBetween(Landmark p1, Landmark vertex, Landmark p2)
    {
        var v1 = new { x = p1.X - vertex.X, y = p1.Y - vertex.Y };
        var v2 = new { x = p2.X - vertex.X, y = p2.Y - vertex.Y };

        double dot = v1.x * v2.x + v1.y * v2.y;
        double mag1 = Math.Sqrt(v1.x * v1.x + v1.y * v1.y);
        double mag2 = Math.Sqrt(v2.x * v2.x + v2.y * v2.y);

        if (mag1 == 0 || mag2 == 0) return 0;

        double cosAngle = dot / (mag1 * mag2);
        cosAngle = Math.Clamp(cosAngle, -1, 1);

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

        double cosAngle = dot / (mag1 * mag2);
        cosAngle = Math.Clamp(cosAngle, -1, 1);

        return Math.Acos(cosAngle) * (180.0 / Math.PI);
    }

    private static double Distance(Landmark p1, Landmark p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
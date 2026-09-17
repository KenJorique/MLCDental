namespace ClinicApp.Services;

/// <summary>
/// Finds the calibration ruler and derives pixels-per-millimetre from its tick
/// spacing. Heuristic, not a trained detector — it looks for a narrow vertical
/// band containing evenly spaced bright marks. Returns a confidence so the app
/// can fall back to manual calibration when it isn't sure.
/// </summary>
public static class RulerScaleDetector
{
    /// <summary>
    /// ASSUMPTION: major ruler ticks are 10mm apart. Verify against a real
    /// X-ray with known markings — if your clinic's ruler uses a different
    /// interval, every derived millimetre value will be wrong by a fixed factor.
    /// </summary>
    public const double DefaultTickSpacingMm = 10.0;

    public static (double? PixelsPerMm, double Confidence) Detect(
        GrayImage image, double knownTickSpacingMm = DefaultTickSpacingMm)
    {
        int w = image.Width, h = image.Height;
        int bandWidth = Math.Max(20, w / 20);
        int stride = Math.Max(1, bandWidth / 2);

        double bestScore = 0;
        List<double> bestTicks = new();

        for (int colStart = 0; colStart + bandWidth < w; colStart += stride)
        {
            var profile = new double[h];
            for (int y = 0; y < h; y++)
            {
                double sum = 0;
                for (int x = colStart; x < colStart + bandWidth; x++)
                    sum += image.Pixels[y * w + x];
                profile[y] = sum / bandWidth;
            }

            double mean = profile.Average();
            double std = Math.Sqrt(profile.Sum(v => (v - mean) * (v - mean)) / h);
            double threshold = mean + std * 1.2;

            var ticks = new List<double>();
            bool inRun = false;
            int runStart = 0;
            for (int y = 0; y < h; y++)
            {
                bool above = profile[y] > threshold;
                if (above && !inRun) { inRun = true; runStart = y; }
                else if (!above && inRun) { inRun = false; ticks.Add((runStart + y) / 2.0); }
            }

            if (ticks.Count < 4) continue;

            var spacings = new List<double>();
            for (int i = 1; i < ticks.Count; i++) spacings.Add(ticks[i] - ticks[i - 1]);

            double median = Median(spacings);
            if (median <= 0) continue;

            double spacingMean = spacings.Average();
            double spacingStd = Math.Sqrt(spacings.Sum(v => (v - spacingMean) * (v - spacingMean)) / spacings.Count);

            // A real ruler has even spacing; random bright texture does not
            double consistency = 1.0 - Math.Min(1.0, spacingStd / median);
            double countScore = Math.Min(1.0, ticks.Count / 10.0);
            double score = consistency * 0.7 + countScore * 0.3;

            if (score > bestScore)
            {
                bestScore = score;
                bestTicks = ticks;
            }
        }

        if (bestScore < 0.4 || bestTicks.Count < 4)
            return (null, 0.0);

        var bestSpacings = new List<double>();
        for (int i = 1; i < bestTicks.Count; i++) bestSpacings.Add(bestTicks[i] - bestTicks[i - 1]);

        double medianSpacingPx = Median(bestSpacings);
        return (medianSpacingPx / knownTickSpacingMm, Math.Round(bestScore, 3));
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }
}
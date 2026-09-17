using ClinicApp.Services.CephaTrain;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace ClinicApp.Services;

/// <summary>
/// Grayscale image buffer plus the pixel operations the Python server used to
/// do with OpenCV/PIL. Everything here works on a plain byte[] so no OpenCV
/// dependency is needed on device.
/// </summary>
public class GrayImage
{
    public byte[] Pixels { get; }
    public int Width { get; }
    public int Height { get; }

    public GrayImage(byte[] pixels, int width, int height)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
    }

    public byte this[int x, int y] => Pixels[y * Width + x];

    public static GrayImage Load(string path)
    {
        using var img = Image.Load<L8>(path);
        int w = img.Width, h = img.Height;
        var buffer = new byte[w * h];

        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                    buffer[y * w + x] = row[x].PackedValue;
            }
        });

        return new GrayImage(buffer, w, h);
    }

    public GrayImage Clone() => new((byte[])Pixels.Clone(), Width, Height);
}

public static class ImageOps
{
    /// <summary>
    /// CLAHE — contrast-limited adaptive histogram equalisation. Port of
    /// cv2.createCLAHE(clipLimit, tileGridSize) from the FastAPI server, kept
    /// because the model's real-world accuracy was tuned with this applied.
    /// Tile histograms are clipped, the excess redistributed, then per-pixel
    /// output is bilinearly interpolated between the four surrounding tile LUTs.
    /// </summary>
    public static GrayImage Clahe(GrayImage src, double clipLimit = 2.5, int tilesX = 8, int tilesY = 8)
    {
        int w = src.Width, h = src.Height;
        int tileW = Math.Max(1, w / tilesX);
        int tileH = Math.Max(1, h / tilesY);

        var luts = new byte[tilesY][][];
        for (int ty = 0; ty < tilesY; ty++)
        {
            luts[ty] = new byte[tilesX][];
            for (int tx = 0; tx < tilesX; tx++)
            {
                int x0 = tx * tileW;
                int y0 = ty * tileH;
                int x1 = (tx == tilesX - 1) ? w : x0 + tileW;
                int y1 = (ty == tilesY - 1) ? h : y0 + tileH;

                var hist = new int[256];
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        hist[src.Pixels[y * w + x]]++;

                int tilePixels = (x1 - x0) * (y1 - y0);
                luts[ty][tx] = BuildClippedLut(hist, tilePixels, clipLimit);
            }
        }

        var outPixels = new byte[w * h];
        for (int y = 0; y < h; y++)
        {
            double fy = (y + 0.5) / tileH - 0.5;
            int ty0 = (int)Math.Floor(fy);
            double wy = fy - ty0;
            int tyA = Math.Clamp(ty0, 0, tilesY - 1);
            int tyB = Math.Clamp(ty0 + 1, 0, tilesY - 1);

            for (int x = 0; x < w; x++)
            {
                double fx = (x + 0.5) / tileW - 0.5;
                int tx0 = (int)Math.Floor(fx);
                double wx = fx - tx0;
                int txA = Math.Clamp(tx0, 0, tilesX - 1);
                int txB = Math.Clamp(tx0 + 1, 0, tilesX - 1);

                byte v = src.Pixels[y * w + x];

                double topLeft = luts[tyA][txA][v];
                double topRight = luts[tyA][txB][v];
                double bottomLeft = luts[tyB][txA][v];
                double bottomRight = luts[tyB][txB][v];

                double top = topLeft + (topRight - topLeft) * wx;
                double bottom = bottomLeft + (bottomRight - bottomLeft) * wx;
                double result = top + (bottom - top) * wy;

                outPixels[y * w + x] = (byte)Math.Clamp(result, 0, 255);
            }
        }

        return new GrayImage(outPixels, w, h);
    }

    private static byte[] BuildClippedLut(int[] hist, int tilePixels, double clipLimit)
    {
        int limit = Math.Max(1, (int)(clipLimit * tilePixels / 256.0));

        long excess = 0;
        for (int i = 0; i < 256; i++)
        {
            if (hist[i] > limit)
            {
                excess += hist[i] - limit;
                hist[i] = limit;
            }
        }

        int bonus = (int)(excess / 256);
        long remainder = excess - (long)bonus * 256;
        for (int i = 0; i < 256; i++) hist[i] += bonus;
        for (int i = 0; i < remainder; i++) hist[i]++;

        var lut = new byte[256];
        long cumulative = 0;
        double scale = 255.0 / Math.Max(1, tilePixels);
        for (int i = 0; i < 256; i++)
        {
            cumulative += hist[i];
            lut[i] = (byte)Math.Clamp(cumulative * scale, 0, 255);
        }
        return lut;
    }

    /// <summary>Separable 5x5 Gaussian, equivalent to cv2.GaussianBlur((5,5), 0).</summary>
    public static GrayImage GaussianBlur5(GrayImage src)
    {
        int w = src.Width, h = src.Height;
        int[] k = { 1, 4, 6, 4, 1 };
        const int kSum = 16;

        var temp = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int acc = 0;
                for (int i = -2; i <= 2; i++)
                    acc += src.Pixels[y * w + Math.Clamp(x + i, 0, w - 1)] * k[i + 2];
                temp[y * w + x] = (byte)(acc / kSum);
            }

        var outPixels = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int acc = 0;
                for (int i = -2; i <= 2; i++)
                    acc += temp[Math.Clamp(y + i, 0, h - 1) * w + x] * k[i + 2];
                outPixels[y * w + x] = (byte)(acc / kSum);
            }

        return new GrayImage(outPixels, w, h);
    }

    /// <summary>Otsu's automatic threshold — same method cv2.THRESH_OTSU uses.</summary>
    public static bool[] OtsuThreshold(GrayImage src)
    {
        var hist = new int[256];
        foreach (var p in src.Pixels) hist[p]++;

        int total = src.Pixels.Length;
        double sum = 0;
        for (int i = 0; i < 256; i++) sum += i * (double)hist[i];

        double sumB = 0;
        int wB = 0;
        double maxVariance = -1;
        int threshold = 0;

        for (int t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0) continue;
            int wF = total - wB;
            if (wF == 0) break;

            sumB += t * (double)hist[t];
            double mB = sumB / wB;
            double mF = (sum - sumB) / wF;
            double variance = (double)wB * wF * (mB - mF) * (mB - mF);

            if (variance > maxVariance)
            {
                maxVariance = variance;
                threshold = t;
            }
        }

        var mask = new bool[src.Pixels.Length];
        for (int i = 0; i < mask.Length; i++) mask[i] = src.Pixels[i] > threshold;
        return mask;
    }

    public static bool[] Dilate(bool[] mask, int w, int h, int radius)
    {
        var result = new bool[mask.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool any = false;
                for (int dy = -radius; dy <= radius && !any; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (mask[ny * w + nx]) { any = true; break; }
                    }
                result[y * w + x] = any;
            }
        return result;
    }

    public static bool[] Erode(bool[] mask, int w, int h, int radius)
    {
        var result = new bool[mask.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool all = true;
                for (int dy = -radius; dy <= radius && all; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = Math.Clamp(x + dx, 0, w - 1);
                        int ny = Math.Clamp(y + dy, 0, h - 1);
                        if (!mask[ny * w + nx]) { all = false; break; }
                    }
                result[y * w + x] = all;
            }
        return result;
    }

    /// <summary>
    /// Largest connected foreground blob, rejecting any component covering more
    /// than maxAreaFraction of the image (border/frame artefacts) — same intent
    /// as the contour-area filter in the Python version.
    /// </summary>
    public static bool[]? LargestComponent(bool[] mask, int w, int h, double maxAreaFraction = 0.92)
    {
        var labels = new int[mask.Length];
        int currentLabel = 0;
        int bestLabel = -1;
        int bestArea = 0;
        long imageArea = (long)w * h;
        var stack = new Stack<int>();

        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || labels[start] != 0) continue;

            currentLabel++;
            int area = 0;
            stack.Push(start);
            labels[start] = currentLabel;

            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                area++;
                int cx = idx % w, cy = idx / w;

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int nIdx = ny * w + nx;
                        if (!mask[nIdx] || labels[nIdx] != 0) continue;
                        labels[nIdx] = currentLabel;
                        stack.Push(nIdx);
                    }
            }

            if (area > bestArea && area < imageArea * maxAreaFraction)
            {
                bestArea = area;
                bestLabel = currentLabel;
            }
        }

        if (bestLabel < 0) return null;

        var result = new bool[mask.Length];
        for (int i = 0; i < mask.Length; i++) result[i] = labels[i] == bestLabel;
        return result;
    }
}

/// <summary>
/// Traces the facial soft-tissue profile (forehead → nose → lips → chin → neck).
/// Direct port of extract_soft_tissue_outline() from the FastAPI server: isolate
/// the skull as one connected blob first, then scan each row inside that blob
/// for the rightmost pixel. Restricting the scan to the blob is what keeps the
/// ruler graphic and any border from being picked up as "rightmost".
/// </summary>
public static class SoftTissueOutlineExtractor
{
    public static List<OutlinePoint> Extract(GrayImage image, int maxPoints = 150)
    {
        int w = image.Width, h = image.Height;
        int margin = Math.Max(2, (int)(Math.Min(h, w) * 0.01));

        int cw = w - margin * 2;
        int ch = h - margin * 2;
        if (cw <= 0 || ch <= 0) return new List<OutlinePoint>();

        var cropped = new byte[cw * ch];
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                cropped[y * cw + x] = image.Pixels[(y + margin) * w + (x + margin)];

        var croppedImage = new GrayImage(cropped, cw, ch);
        var blurred = ImageOps.GaussianBlur5(croppedImage);
        var mask = ImageOps.OtsuThreshold(blurred);

        // Morphological close then open, matching the 7x7 kernel in Python
        mask = ImageOps.Erode(ImageOps.Dilate(mask, cw, ch, 3), cw, ch, 3);
        mask = ImageOps.Dilate(ImageOps.Erode(mask, cw, ch, 3), cw, ch, 3);

        var blob = ImageOps.LargestComponent(mask, cw, ch);
        if (blob == null) return new List<OutlinePoint>();

        int top = ch, bottom = 0;
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                if (blob[y * cw + x])
                {
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                    break;
                }

        if (bottom <= top) return new List<OutlinePoint>();

        int blobHeight = bottom - top;
        int scanTop = top + (int)(blobHeight * 0.02);
        int scanBottom = top + (int)(blobHeight * 0.98);

        var xs = new List<float>();
        var ys = new List<float>();

        for (int y = scanTop; y < scanBottom; y++)
        {
            int rightmost = -1;
            for (int x = cw - 1; x >= 0; x--)
                if (blob[y * cw + x]) { rightmost = x; break; }

            if (rightmost < 0) continue;
            xs.Add(rightmost);
            ys.Add(y);
        }

        if (xs.Count < 2) return new List<OutlinePoint>();

        // Moving-average smoothing so the curve doesn't jitter onto bone edges
        const int window = 9;
        var smoothed = new List<OutlinePoint>();
        for (int i = 0; i < xs.Count; i++)
        {
            int start = Math.Max(0, i - window / 2);
            int end = Math.Min(xs.Count - 1, i + window / 2);
            float sum = 0;
            for (int j = start; j <= end; j++) sum += xs[j];
            float avgX = sum / (end - start + 1);

            smoothed.Add(new OutlinePoint { X = avgX + margin, Y = ys[i] + margin });
        }

        if (smoothed.Count > maxPoints)
        {
            int step = Math.Max(1, smoothed.Count / maxPoints);
            smoothed = smoothed.Where((_, i) => i % step == 0).ToList();
        }

        return smoothed;
    }
}
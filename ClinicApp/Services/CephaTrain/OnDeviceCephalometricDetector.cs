using ClinicApp.Services.CephaTrain;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClinicApp.Services.CephaTrain;

/// <summary>
/// Runs the YOLOv8 cephalometric landmark model entirely on device, replacing
/// the FastAPI server. Same inputs and same DetectionResult shape as the old
/// HTTP detector, so nothing downstream changes.
/// </summary>
public class OnDeviceCephalometricDetector
{
    private const string ModelAssetPath = "Models/cepha_landmarks.onnx";
    private const int InputSize = 800;          // must match training imgsz=800
    private const float ConfidenceThreshold = 0.15f;
    private const byte LetterboxFill = 114;     // Ultralytics' padding grey

    private InferenceSession? _session;
    private string _inputName = "images";
    private string _outputName = "output0";
    private string[] _classNames = Array.Empty<string>();
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static readonly string[] PlaneNames =
    {
        "S-N line", "N-A line", "N-B line", "Frankfort plane", "Mandibular plane"
    };

    private static readonly Dictionary<string, string[]> PlaneDependencies = new()
    {
        ["S-N line"] = new[] { "Sella", "Nasion" },
        ["N-A line"] = new[] { "Nasion", "Subspinale" },
        ["N-B line"] = new[] { "Nasion", "Supramentale" },
        ["Frankfort plane"] = new[] { "Porion", "Orbitale" },
        ["Mandibular plane"] = new[] { "Gonion", "Menton" },
    };

    public async Task InitializeAsync()
    {
        if (_session != null) return;

        await _initLock.WaitAsync();
        try
        {
            if (_session != null) return;

            using var stream = await FileSystem.OpenAppPackageFileAsync(ModelAssetPath);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
            _session = new InferenceSession(memory.ToArray(), options);

            _inputName = _session.InputMetadata.Keys.First();
            _outputName = _session.OutputMetadata.Keys.First();
            _classNames = ReadClassNames(_session);

            System.Diagnostics.Debug.WriteLine($"✅ ONNX model loaded — {_classNames.Length} classes, input '{_inputName}', output '{_outputName}'");
        }
        catch (Exception ex)
        {
            // A TypeInitializationException here means the native onnxruntime
            // library didn't load. The outer message never says why — the
            // inner exception does (missing .so, wrong ABI, trimmed away).
            System.Diagnostics.Debug.WriteLine($"❌ ONNX init failed: {ex.GetType().Name} — {ex.Message}");
            var inner = ex.InnerException;
            int depth = 0;
            while (inner != null && depth++ < 5)
            {
                System.Diagnostics.Debug.WriteLine($"   ↳ inner[{depth}]: {inner.GetType().Name} — {inner.Message}");
                inner = inner.InnerException;
            }
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Class names come from the model's own metadata where possible, so the
    /// id→name mapping can never drift out of sync with a retrained model.
    /// Falls back to the data.yaml order only if the metadata is absent.
    /// </summary>
    private static string[] ReadClassNames(InferenceSession session)
    {
        if (session.ModelMetadata.CustomMetadataMap.TryGetValue("names", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            var found = new SortedDictionary<int, string>();
            foreach (Match m in Regex.Matches(raw, @"(\d+)\s*:\s*['""]([^'""]+)['""]"))
                found[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value;

            if (found.Count > 0) return found.Values.ToArray();
        }

        System.Diagnostics.Debug.WriteLine("⚠ ONNX metadata has no class names — falling back to ApiConfig.LandmarkClassOrder");
        return ClinicApp.Config.ApiConfig.LandmarkClassOrder.ToArray();
    }

    public async Task<DetectionResult> DetectLandmarksAsync(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"Image not found: {imagePath}");

        await InitializeAsync();
        if (_session == null)
            throw new InvalidOperationException("ONNX session failed to initialise");

        return await Task.Run(() => RunDetection(imagePath));
    }

    private DetectionResult RunDetection(string imagePath)
    {
        var original = GrayImage.Load(imagePath);
        var enhanced = ImageOps.Clahe(original);

        var (tensor, scale, padX, padY) = Letterbox(enhanced);

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, tensor) };
        using var results = _session!.Run(inputs);

        var output = results.First(r => r.Name == _outputName).AsTensor<float>();
        var raw = DecodeDetections(output, scale, padX, padY);

        System.Diagnostics.Debug.WriteLine($"Raw detections before per-class filtering: {raw.Count}");
        foreach (var d in raw)
            System.Diagnostics.Debug.WriteLine($"  {d.ClassName}: conf={d.Confidence:F3}");

        // Each cephalometric landmark exists exactly once — keep the strongest
        var bestPerClass = new Dictionary<string, Landmark>();
        foreach (var det in raw)
        {
            var name = det.ClassName ?? "";
            if (!bestPerClass.TryGetValue(name, out var existing) || det.Confidence > existing.Confidence)
                bestPerClass[name] = det;
        }

        var landmarks = bestPerClass.Values.ToList();
        for (int i = 0; i < landmarks.Count; i++) landmarks[i].Index = i + 1;

        var detectedNames = landmarks.Select(l => l.ClassName ?? "").ToHashSet();
        var missing = _classNames.Where(n => !detectedNames.Contains(n)).ToList();
        var incompletePlanes = PlaneNames
            .Where(p => PlaneDependencies[p].Any(req => !detectedNames.Contains(req)))
            .ToList();

        var outline = SoftTissueOutlineExtractor.Extract(original);
        var (pixelsPerMm, rulerConfidence) = RulerScaleDetector.Detect(original);

        System.Diagnostics.Debug.WriteLine($"Ruler detection: pixels_per_mm={pixelsPerMm}, confidence={rulerConfidence}");

        return new DetectionResult
        {
            Landmarks = landmarks,
            SoftTissueOutline = outline,
            MissingLandmarks = missing,
            IncompletePlanes = incompletePlanes,
            PixelsPerMm = pixelsPerMm,
            RulerConfidence = rulerConfidence
        };
    }

    /// <summary>
    /// Ultralytics-style letterbox: scale to fit inside InputSize preserving
    /// aspect ratio, pad the remainder with grey, centred. The scale and pad
    /// offsets are returned so detections can be mapped back to original
    /// image pixels.
    /// </summary>
    private static (DenseTensor<float> Tensor, float Scale, float PadX, float PadY) Letterbox(GrayImage src)
    {
        float scale = Math.Min((float)InputSize / src.Width, (float)InputSize / src.Height);
        int newW = (int)Math.Round(src.Width * scale);
        int newH = (int)Math.Round(src.Height * scale);
        float padX = (InputSize - newW) / 2f;
        float padY = (InputSize - newH) / 2f;

        var tensor = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });

        for (int y = 0; y < InputSize; y++)
        {
            for (int x = 0; x < InputSize; x++)
            {
                byte value;

                int sx = (int)((x - padX) / scale);
                int sy = (int)((y - padY) / scale);

                if (x < padX || y < padY || sx < 0 || sy < 0 || sx >= src.Width || sy >= src.Height)
                    value = LetterboxFill;
                else
                    value = src.Pixels[sy * src.Width + sx];

                float normalized = value / 255f;
                tensor[0, 0, y, x] = normalized;
                tensor[0, 1, y, x] = normalized;
                tensor[0, 2, y, x] = normalized;
            }
        }

        return (tensor, scale, padX, padY);
    }

    /// <summary>
    /// YOLOv8 output is [1, 4 + numClasses, numAnchors]: four box values then
    /// one score per class, per anchor.
    /// NOTE ON COORDINATES: the FastAPI server reported box.xyxy[0][0..1] — the
    /// box's top-left corner, not its centre — and every saved measurement and
    /// region guide was calibrated against that. This reproduces it exactly so
    /// results stay consistent with what the app already produces.
    /// </summary>
    private List<Landmark> DecodeDetections(Tensor<float> output, float scale, float padX, float padY)
    {
        int channels = output.Dimensions[1];
        int anchors = output.Dimensions[2];
        int numClasses = channels - 4;

        var detections = new List<Landmark>();

        for (int a = 0; a < anchors; a++)
        {
            int bestClass = -1;
            float bestScore = ConfidenceThreshold;

            for (int c = 0; c < numClasses; c++)
            {
                float score = output[0, 4 + c, a];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestClass < 0) continue;

            float cx = output[0, 0, a];
            float cy = output[0, 1, a];
            float bw = output[0, 2, a];
            float bh = output[0, 3, a];

            float x1 = cx - bw / 2f;
            float y1 = cy - bh / 2f;

            float originalX = (x1 - padX) / scale;
            float originalY = (y1 - padY) / scale;

            detections.Add(new Landmark
            {
                X = originalX,
                Y = originalY,
                Confidence = bestScore,
                ClassId = bestClass,
                ClassName = bestClass < _classNames.Length ? _classNames[bestClass] : $"class_{bestClass}"
            });
        }

        return detections;
    }
}
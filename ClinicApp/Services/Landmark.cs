using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ClinicApp.Services;

public class Landmark : INotifyPropertyChanged
{
    private float _x;
    private float _y;

    [JsonIgnore]
    public bool IsManuallyPlaced { get; set; }

    [JsonPropertyName("x")]
    public float X
    {
        get => _x;
        set
        {
            if (_x != value)
            {
                _x = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonPropertyName("y")]
    public float Y
    {
        get => _y;
        set
        {
            if (_y != value)
            {
                _y = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("class_id")]
    public int ClassId { get; set; }

    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; }

    // Display order number shown on the canvas and in the list badge — set in AnalyzeImage()
    public int Index { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public override string ToString() => $"{ClassName}: ({X:F1}, {Y:F1}) conf={Confidence:F2}";
}

public static class LandmarkColors
{
    private static readonly Color[] Palette = new[]
    {
        Colors.Red, Colors.Blue, Colors.Green, Colors.Orange, Colors.Purple,
        Colors.Teal, Colors.Magenta, Colors.DarkGoldenrod, Colors.DeepPink,
        Colors.Lime, Colors.Indigo, Colors.Brown, Colors.Cyan, Colors.Crimson,
        Colors.SlateBlue, Colors.OliveDrab, Colors.Coral, Colors.SteelBlue, Colors.HotPink
    };

    public static Color GetColor(int classId) =>
        Palette[classId % Palette.Length];
}

/// <summary>
/// Approximate normalized (0-1) region where each landmark is typically found
/// on a standard right-facing lateral cephalogram. These are rough guides for
/// manual placement, NOT precise targets — real anatomy varies by patient and
/// by how tightly the X-ray is cropped. CenterX/Y and RadiusFraction define a
/// soft "expected zone" circle, not a hard boundary.
/// </summary>
public static class LandmarkRegionGuide
{
    public record Region(float CenterX, float CenterY, float RadiusFraction);

    private static readonly Dictionary<string, Region> Regions = new()
    {
        ["Sella"] = new(0.40f, 0.30f, 0.08f),
        ["Nasion"] = new(0.62f, 0.20f, 0.07f),
        ["Orbitale"] = new(0.50f, 0.36f, 0.07f),
        ["Porion"] = new(0.30f, 0.36f, 0.07f),
        ["Articulare"] = new(0.32f, 0.44f, 0.07f),
        ["Anterior nasal spine"] = new(0.62f, 0.47f, 0.06f),
        ["Posterior nasal spine"] = new(0.48f, 0.48f, 0.06f),
        ["Subspinale"] = new(0.62f, 0.50f, 0.06f),
        ["Subnasale"] = new(0.64f, 0.49f, 0.06f),
        ["Upper lip"] = new(0.66f, 0.55f, 0.06f),
        ["Incision superius"] = new(0.63f, 0.55f, 0.06f),
        ["Incision inferius"] = new(0.62f, 0.58f, 0.06f),
        ["Lower lip"] = new(0.65f, 0.60f, 0.06f),
        ["Supramentale"] = new(0.58f, 0.65f, 0.06f),
        ["Gonion"] = new(0.30f, 0.62f, 0.08f),
        ["Pogonion"] = new(0.60f, 0.75f, 0.06f),
        ["Soft tissue pogonion"] = new(0.63f, 0.75f, 0.06f),
        ["Gnathion"] = new(0.58f, 0.78f, 0.06f),
        ["Menton"] = new(0.56f, 0.80f, 0.06f),
    };

    public static Region? GetRegion(string landmarkName) =>
        Regions.TryGetValue(landmarkName, out var r) ? r : null;
}   
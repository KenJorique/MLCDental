using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ClinicApp.Services;

public class Landmark : INotifyPropertyChanged
{
    private float _x;
    private float _y;

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
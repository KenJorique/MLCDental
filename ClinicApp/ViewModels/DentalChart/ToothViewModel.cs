using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.DentalChart;

public partial class ToothViewModel : ObservableObject
{
    // ═══════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int toothNumber;

    [ObservableProperty]
    private string condition = "Normal";

    [ObservableProperty]
    private Color toothColor = Colors.White;

    [ObservableProperty]
    private Color toothIconColor =
        Color.FromArgb("#555555");

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private string lastUpdated = string.Empty;

    public string ToothLabel =>
        ToothNumber.ToString();

    // ═══════════════════════════════════════════════════════════════
    // TOOTH NAME
    // ═══════════════════════════════════════════════════════════════

    public string ToothName => ToothNumber switch
    {
        1 => "UR8 · 3rd Molar",
        2 => "UR7 · 2nd Molar",
        3 => "UR6 · 1st Molar",
        4 => "UR5 · 2nd Premolar",
        5 => "UR4 · 1st Premolar",
        6 => "UR3 · Canine",
        7 => "UR2 · Lat. Incisor",
        8 => "UR1 · Cen. Incisor",

        9 => "UL1 · Cen. Incisor",
        10 => "UL2 · Lat. Incisor",
        11 => "UL3 · Canine",
        12 => "UL4 · 1st Premolar",
        13 => "UL5 · 2nd Premolar",
        14 => "UL6 · 1st Molar",
        15 => "UL7 · 2nd Molar",
        16 => "UL8 · 3rd Molar",

        17 => "LL8 · 3rd Molar",
        18 => "LL7 · 2nd Molar",
        19 => "LL6 · 1st Molar",
        20 => "LL5 · 2nd Premolar",
        21 => "LL4 · 1st Premolar",
        22 => "LL3 · Canine",
        23 => "LL2 · Lat. Incisor",
        24 => "LL1 · Cen. Incisor",

        25 => "LR1 · Cen. Incisor",
        26 => "LR2 · Lat. Incisor",
        27 => "LR3 · Canine",
        28 => "LR4 · 1st Premolar",
        29 => "LR5 · 2nd Premolar",
        30 => "LR6 · 1st Molar",
        31 => "LR7 · 2nd Molar",
        32 => "LR8 · 3rd Molar",

        _ => $"Tooth {ToothNumber}"
    };

    // ═══════════════════════════════════════════════════════════════
    // APPLY TOOTH RECORD
    // ═══════════════════════════════════════════════════════════════

    public void ApplyRecord(ToothRecord record)
    {
        Condition =
            string.IsNullOrWhiteSpace(record.Condition)
                ? "Normal"
                : record.Condition;

        Notes =
            record.Notes ?? string.Empty;

        LastUpdated =
            record.LastUpdated ?? string.Empty;

        SetColorFromHex(record.Color);
    }

    // ═══════════════════════════════════════════════════════════════
    // RESET
    // ═══════════════════════════════════════════════════════════════

    public void Reset()
    {
        Condition = "Normal";

        Notes =
            string.Empty;

        LastUpdated =
            string.Empty;

        ToothColor =
            Colors.White;

        ToothIconColor =
            Color.FromArgb("#444444");
    }

    // ═══════════════════════════════════════════════════════════════
    // COLOR
    // ═══════════════════════════════════════════════════════════════

    private void SetColorFromHex(string? hex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                ToothColor = Colors.White;
                ToothIconColor =
                    Color.FromArgb("#444444");

                return;
            }

            var color =
                Color.FromArgb(hex);

            ToothColor =
                color;

            // White text/icon on dark teeth
            if (IsDarkColor(color))
            {
                ToothIconColor =
                    Colors.White;
            }
            else
            {
                ToothIconColor =
                    Color.FromArgb("#444444");
            }
        }
        catch
        {
            ToothColor =
                Colors.White;

            ToothIconColor =
                Color.FromArgb("#444444");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // DETERMINE IF COLOR IS DARK
    // ═══════════════════════════════════════════════════════════════

    private static bool IsDarkColor(Color color)
    {
        var luminance =
            (0.299 * color.Red)
            + (0.587 * color.Green)
            + (0.114 * color.Blue);

        return luminance < 0.45;
    }
}
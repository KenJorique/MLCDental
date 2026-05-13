using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.DentalChart;

public partial class ToothViewModel : ObservableObject
{
    [ObservableProperty] private int toothNumber;
    [ObservableProperty] private string condition = "Normal";
    [ObservableProperty] private Color toothColor = Colors.White;
    [ObservableProperty] private Color toothIconColor = Color.FromArgb("#555555");
    [ObservableProperty] private bool isSelected;

    public string ToothLabel => ToothNumber.ToString();

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

    public void ApplyRecord(ToothRecord record)
    {
        Condition = record.Condition;
        SetColor(Color.FromArgb(record.Color));
    }

    public void Reset()
    {
        Condition = "Normal";
        SetColor(Colors.White);
    }

    private void SetColor(Color bg)
    {
        ToothColor = bg;
        ToothIconColor = bg == Colors.Black ? Colors.White : Color.FromArgb("#444444");
    }
}

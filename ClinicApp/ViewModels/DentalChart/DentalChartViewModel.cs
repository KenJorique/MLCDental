using ClinicApp.Models;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.DentalChart;

[QueryProperty(nameof(PatientId), "patientId")]
[QueryProperty(nameof(PatientName), "patientName")]
public partial class DentalChartViewModel : ObservableObject
{
    // ── Legend ────────────────────────────────────────────────────
    public static readonly Dictionary<string, string> ConditionColors = new()
    {
        { "Normal",    "#FFFFFF" },
        { "Filling",   "#0000FF" },  // Blue  – Existing Restoration
        { "Caries",    "#FF0000" },  // Red   – Active Decay
        { "Completed", "#00FF00" },  // Green – Completed Treatment
        { "Missing",   "#000000" },  // Black – Missing / Extracted
    };

    private readonly DatabaseService _db;

    [ObservableProperty] private int patientId;
    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;

    // All 32 teeth
    public ObservableCollection<ToothViewModel> UpperTeeth { get; } = new();
    public ObservableCollection<ToothViewModel> LowerTeeth { get; } = new();

    // Private flat list for fast lookups
    private readonly List<ToothViewModel> _allTeeth = new();

    public DentalChartViewModel(DatabaseService db)
    {
        _db = db;
        BuildTeeth();
    }

    private void BuildTeeth()
    {
        for (int i = 1; i <= 32; i++)
        {
            var t = new ToothViewModel { ToothNumber = i };
            _allTeeth.Add(t);
            if (i <= 16) UpperTeeth.Add(t);
            else LowerTeeth.Add(t);
        }
    }

    // QueryProperty hook – fires when navigation injects PatientId
    partial void OnPatientIdChanged(int value)
    {
        if (value > 0)
            LoadChartCommand.ExecuteAsync(null);
    }

    // ── Load / repaint ────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadChartAsync()
    {
        if (PatientId <= 0) return;
        IsBusy = true;
        try
        {
            foreach (var t in _allTeeth) t.Reset();

            var records = await _db.GetToothRecordsForPatient(PatientId);
            foreach (var rec in records)
                _allTeeth.FirstOrDefault(t => t.ToothNumber == rec.ToothNumber)
                         ?.ApplyRecord(rec);

            StatusMessage = records.Count > 0
                ? $"{records.Count} condition(s) on record."
                : "No chart history yet — tap a tooth to begin.";
        }
        finally { IsBusy = false; }
    }

    // ── Tooth tap ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToothTapped(int toothNumber)
    {
        if (IsBusy) return;

        var tooth = _allTeeth.FirstOrDefault(t => t.ToothNumber == toothNumber);
        if (tooth is null) return;

        // Highlight selection
        foreach (var t in _allTeeth) t.IsSelected = false;
        tooth.IsSelected = true;

        // Build action sheet excluding the current condition
        var options = ConditionColors.Keys
            .Where(k => k != tooth.Condition)
            .ToArray();

        string action = await Shell.Current.DisplayActionSheet(
            $"Tooth #{toothNumber}  ·  {tooth.ToothName}",
            "Cancel", null, options);

        tooth.IsSelected = false;

        if (string.IsNullOrWhiteSpace(action) || action == "Cancel") return;

        await ApplyConditionAsync(tooth, action);
    }

    private async Task ApplyConditionAsync(ToothViewModel tooth, string condition)
    {
        IsBusy = true;
        try
        {
            var hex = ConditionColors[condition];
            tooth.ApplyRecord(new ToothRecord
            {
                PatientId = PatientId,
                ToothNumber = tooth.ToothNumber,
                Condition = condition,
                Color = hex,
            });

            await _db.SaveToothRecord(new ToothRecord
            {
                PatientId = PatientId,
                ToothNumber = tooth.ToothNumber,
                Condition = condition,
                Color = hex,
            });

            StatusMessage = $"✔  Tooth #{tooth.ToothNumber}: {condition} saved.";
        }
        finally { IsBusy = false; }
    }

    // ── Reset single tooth ────────────────────────────────────────

    [RelayCommand]
    private async Task ResetTooth(int toothNumber)
    {
        IsBusy = true;
        try
        {
            _allTeeth.FirstOrDefault(t => t.ToothNumber == toothNumber)?.Reset();
            await _db.DeleteToothRecord(PatientId, toothNumber);
            StatusMessage = $"Tooth #{toothNumber} cleared.";
        }
        finally { IsBusy = false; }
    }
}

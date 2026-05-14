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
    // ── Legend / condition palette ─────────────────────────────────
    public static readonly Dictionary<string, string> ConditionColors = new()
    {
        { "Normal",    "#FFFFFF" },
        { "Filling",   "#0000FF" },
        { "Caries",    "#FF0000" },
        { "Completed", "#00FF00" },
        { "Missing",   "#000000" },
    };

    /// <summary>Exposed to the Picker in the modal's edit view.</summary>
    public List<string> ConditionOptions { get; } = new(ConditionColors.Keys);

    private readonly DatabaseService _db;

    // ── Page-level state ──────────────────────────────────────────
    [ObservableProperty] private int patientId;
    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;

    // ── Tooth collections ─────────────────────────────────────────
    public ObservableCollection<ToothViewModel> UpperTeeth { get; } = new();
    public ObservableCollection<ToothViewModel> LowerTeeth { get; } = new();
    private readonly List<ToothViewModel> _allTeeth = new();

    // ── Modal state ───────────────────────────────────────────────
    [ObservableProperty] private bool isModalVisible;
    [ObservableProperty] private bool isEditMode;

    // Read-only display fields
    [ObservableProperty] private string modalToothTitle = string.Empty;
    [ObservableProperty] private string modalToothName = string.Empty;
    [ObservableProperty] private string modalCondition = string.Empty;
    [ObservableProperty] private Color modalConditionColor = Colors.White;
    [ObservableProperty] private string modalLastUpdated = string.Empty;
    [ObservableProperty] private string modalNotes = string.Empty;

    // Editable fields (bound to Picker / Editor in edit mode)
    [ObservableProperty] private string editCondition = string.Empty;
    [ObservableProperty] private string editNotes = string.Empty;

    // The tooth whose modal is currently open
    private ToothViewModel? _modalTooth;

    // ─────────────────────────────────────────────────────────────

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

    partial void OnPatientIdChanged(int value)
    {
        if (value > 0) LoadChartCommand.ExecuteAsync(null);
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

    // ── Tooth tap → open modal ────────────────────────────────────

    [RelayCommand]
    private void ToothTapped(int toothNumber)
    {
        if (IsBusy) return;

        var tooth = _allTeeth.FirstOrDefault(t => t.ToothNumber == toothNumber);
        if (tooth is null) return;

        // Highlight
        foreach (var t in _allTeeth) t.IsSelected = false;
        tooth.IsSelected = true;

        // Populate modal read fields
        _modalTooth = tooth;
        ModalToothTitle = $"Tooth #{toothNumber}";
        ModalToothName = tooth.ToothName;
        ModalCondition = tooth.Condition;
        ModalConditionColor = Color.FromArgb(ConditionColors[tooth.Condition]);
        ModalLastUpdated = string.IsNullOrWhiteSpace(tooth.LastUpdated)
                                ? "Not recorded"
                                : tooth.LastUpdated;
        ModalNotes = tooth.Notes;

        // Pre-fill edit fields
        EditCondition = tooth.Condition;
        EditNotes = tooth.Notes;

        IsEditMode = false;
        IsModalVisible = true;
    }

    // ── Modal commands ────────────────────────────────────────────

    [RelayCommand]
    private void CloseModal()
    {
        IsModalVisible = false;
        IsEditMode = false;
        if (_modalTooth is not null) _modalTooth.IsSelected = false;
        _modalTooth = null;
    }

    [RelayCommand]
    private void EnterEditMode() => IsEditMode = true;

    [RelayCommand]
    private void CancelEdit()
    {
        // Restore edit fields to current saved values
        if (_modalTooth is not null)
        {
            EditCondition = _modalTooth.Condition;
            EditNotes = _modalTooth.Notes;
        }
        IsEditMode = false;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (_modalTooth is null || IsBusy) return;
        IsBusy = true;
        try
        {
            var hex = ConditionColors[EditCondition];

            var record = new ToothRecord
            {
                PatientId = PatientId,
                ToothNumber = _modalTooth.ToothNumber,
                Condition = EditCondition,
                Color = hex,
                Notes = EditNotes ?? string.Empty,
            };

            _modalTooth.ApplyRecord(record);
            await _db.SaveToothRecord(record);

            // Refresh modal display fields
            ModalCondition = EditCondition;
            ModalConditionColor = Color.FromArgb(hex);
            ModalLastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd");
            ModalNotes = EditNotes ?? string.Empty;

            StatusMessage = $"✔  Tooth #{_modalTooth.ToothNumber}: {EditCondition} saved.";
            IsEditMode = false;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ClearToothFromModalAsync()
    {
        if (_modalTooth is null || IsBusy) return;
        IsBusy = true;
        try
        {
            int num = _modalTooth.ToothNumber;
            _modalTooth.Reset();
            await _db.DeleteToothRecord(PatientId, num);

            StatusMessage = $"Tooth #{num} cleared.";
            CloseModal();
        }
        finally { IsBusy = false; }
    }

    // ── Legacy reset (still usable elsewhere) ────────────────────

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

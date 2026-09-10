using System.Collections.ObjectModel;

namespace ClinicApp.Models.TreatmentModels;

// Groups all TreatmentHistory records for one visit date into one card on TreatmentHistoryPage.
public class TreatmentVisitGroup
{
    public DateTime VisitDate { get; set; }

    public ObservableCollection<TreatmentHistory> Treatments { get; } = new();

    // Formatted visit date, or blank if unset.
    public string DateDisplay =>
        VisitDate == DateTime.MinValue
            ? string.Empty
            : VisitDate.ToString("MMM dd, yyyy");

    // Time of the first treatment in the visit.
    public string TimeDisplay
    {
        get
        {
            var first = Treatments.FirstOrDefault();
            if (first == null) return string.Empty;
            if (DateTime.TryParse(first.Timestamp, out var dt))
                return dt.ToString("hh:mm tt");
            return string.Empty;
        }
    }

    // One row per treatment/service performed at this visit.
    public List<TreatmentRowItem> Items =>
        Treatments.Select(t => new TreatmentRowItem(t)).ToList();

    // First non-empty note across all treatments in the visit.
    public string SharedNotes =>
        Treatments
            .Select(t => t.Notes ?? string.Empty)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
        ?? string.Empty;

    // Whether this visit has any notes to show.
    public bool HasNotes => !string.IsNullOrWhiteSpace(SharedNotes);

    // "N treatment(s)" summary text.
    public string VisitTitle =>
        Treatments.Count == 1
            ? "1 treatment"
            : $"{Treatments.Count} treatments";
}

// One treatment/service row inside a visit card on TreatmentHistoryPage.
public class TreatmentRowItem
{
    private readonly TreatmentHistory _record;

    // Wraps a single treatment record.
    public TreatmentRowItem(TreatmentHistory record)
    {
        _record = record;
    }

    // Falls back to Condition/ActionType when Description is blank (non-service tooth entries).
    public string Description =>
        !string.IsNullOrWhiteSpace(_record.Description)
            ? _record.Description
            : !string.IsNullOrWhiteSpace(_record.Condition)
                ? _record.Condition
                : _record.ActionType;

    // Tooth name if known, else "#N", else blank for general services.
    public string ToothDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_record.ToothName))
                return _record.ToothName;
            if (_record.ToothNumber > 0)
                return $"#{_record.ToothNumber}";
            return string.Empty;
        }
    }

    // Whether this row has a tooth to show (false for general services).
    public bool HasTooth => !string.IsNullOrWhiteSpace(ToothDisplay);
}

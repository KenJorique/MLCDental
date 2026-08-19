using System.Collections.ObjectModel;

namespace ClinicApp.Models;

/// <summary>
/// Groups all TreatmentHistory records for a single visit date
/// into one card on TreatmentHistoryPage.
/// </summary>
public class TreatmentVisitGroup
{
    public DateTime VisitDate { get; set; }

    public ObservableCollection<TreatmentHistory> Treatments { get; } = new();

    // ── Date / Time display ───────────────────────────────────────
    public string DateDisplay =>
        VisitDate == DateTime.MinValue
            ? string.Empty
            : VisitDate.ToString("MMM dd, yyyy");

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

    // ── Service rows for the new UI ───────────────────────────────
    public List<TreatmentRowItem> Items =>
        Treatments.Select(t => new TreatmentRowItem(t)).ToList();

    // ── Shared notes — first non-empty note in the visit ─────────
    public string SharedNotes =>
        Treatments
            .Select(t => t.Notes ?? string.Empty)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
        ?? string.Empty;

    public bool HasNotes => !string.IsNullOrWhiteSpace(SharedNotes);

    // ── Legacy title used by VisitDetailsPage ─────────────────────
    public string VisitTitle =>
        Treatments.Count == 1
            ? "1 treatment"
            : $"{Treatments.Count} treatments";
}

/// <summary>
/// One service row inside a visit card on TreatmentHistoryPage.
/// </summary>
public class TreatmentRowItem
{
    private readonly TreatmentHistory _record;

    public TreatmentRowItem(TreatmentHistory record)
    {
        _record = record;
    }

    public string Description =>
        _record.Description ?? string.Empty;

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

    public bool HasTooth => !string.IsNullOrWhiteSpace(ToothDisplay);
}

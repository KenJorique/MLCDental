using System.Collections.ObjectModel;

namespace ClinicApp.Models;

public class TreatmentVisitGroup
{
    public DateTime VisitDate { get; set; }

    public string DateDisplay =>
        VisitDate.ToString("MMMM dd, yyyy");

    public ObservableCollection<TreatmentHistory> Treatments { get; }
        = new();

    public string VisitTitle =>
    Treatments.Count == 1
        ? "1 treatment"
        : $"{Treatments.Count} treatments";
}
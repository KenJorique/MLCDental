namespace ClinicApp.Helpers;

using ClinicApp.Models.TreatmentModels;

public static class VisitHistoryStore
{
    public static TreatmentVisitGroup? Current { get; set; }
}
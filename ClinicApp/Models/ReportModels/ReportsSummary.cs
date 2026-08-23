namespace ClinicApp.Models.ReportModels
{
    public class ReportsSummary
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Appointments
        public int TotalAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public int PendingAppointments { get; set; }
        public int CancelledAppointments { get; set; }

        // Treatments
        public int TotalTreatments { get; set; }
        public Dictionary<string, int> TreatmentBreakdown { get; set; } = new();

        // Billing
        public decimal TotalRevenue { get; set; }
        public decimal OutstandingBalance { get; set; }

        // Supplies
        public int LowStockItemCount { get; set; }
        public List<string> LowStockItemNames { get; set; } = new();

        // Services Rendered
        public int TotalServicesRendered { get; set; }
        public Dictionary<string, int> ServiceBreakdown { get; set; } = new();
    }

    public enum ReportPeriod
    {
        Daily,
        Weekly,
        Monthly
    }
}
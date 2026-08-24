namespace ClinicApp.Models.ReportModels
{
    // Plain data holder for one period's report — filled in by
    // ReportsViewModel.LoadReport(). No UI/chart logic lives here on
    // purpose, so this stays reusable if the report is ever shown
    // somewhere other than the Reports page (e.g. exported as PDF later).
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
        public string MostCommonTreatment { get; set; } = "—";

        // Billing
        public decimal TotalRevenue { get; set; }
        public decimal OutstandingBalance { get; set; }

        // Supplies — overview (current snapshot, same regardless of
        // period, since "how many items are low right now" isn't a
        // period-based question).
        public int TotalSupplies { get; set; }
        public int InStockCount { get; set; }
        public int LowStockItemCount { get; set; }
        public int OutOfStockCount { get; set; }
        public List<string> LowStockItemNames { get; set; } = new();

        // Supplies — movement WITHIN the selected period, from
        // supply_stock_logs (restocks add pieces, usage/adjustments
        // remove them). This part DOES change with Daily/Weekly/Monthly.
        public int PiecesRestocked { get; set; }
        public int PiecesUsed { get; set; }

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

    // One selectable entry in the period dropdown — e.g. "Aug 20, 2026"
    // for Daily, or "Aug 3 - Aug 9" for Weekly. Rebuilt whenever the
    // Daily/Weekly/Monthly tab changes; see ReportsViewModel.BuildDateOptions.
    public class DateRangeOption
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    // One bar in the Treatments chart. WidthProportion (0-1) is this
    // condition's count relative to the largest one, used to size the
    // bar's width in the custom horizontal-bar UI (see ReportsPage.xaml)
    // — Microcharts doesn't have a horizontal bar chart type, so this is
    // drawn with plain layout instead of a chart library.
    public class TreatmentBarItem
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public double WidthProportion { get; set; }
    }
}

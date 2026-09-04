namespace ClinicApp.Models.ReportModels
{
    // Plain data holder for one period's report — filled in by
    // ReportsViewModel.LoadReport(). 
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
        public string AppointmentsInsight { get; set; } = string.Empty;

        // Patient names behind each count above — feeds the tap-to-view alert on the summary counts.
        public List<string> CompletedAppointmentNames { get; set; } = new();
        public List<string> PendingAppointmentNames { get; set; } = new();
        public List<string> CancelledAppointmentNames { get; set; } = new();

        // Treatments
        public int TotalTreatments { get; set; }
        public Dictionary<string, int> TreatmentBreakdown { get; set; } = new();
        public string MostCommonTreatment { get; set; } = "—";

        // Short takeaway for the Treatments card, e.g.
        // "Most common: Teeth Cleaning and Whitening — 28% each".
        public string TreatmentsInsight { get; set; } = string.Empty;

        // Billing
        public decimal TotalRevenue { get; set; }
        public decimal OutstandingBalance { get; set; }

        // Short takeaway for the Billing card, e.g.
        // "Top earner: Dental Crown — ₱18,000".
        public string BillingInsight { get; set; } = string.Empty;

        // Supplies 
        public int TotalSupplies { get; set; }
        public int InStockCount { get; set; }
        public int LowStockItemCount { get; set; }
        public int OutOfStockCount { get; set; }
        public List<string> LowStockItemNames { get; set; } = new();
        public List<string> OutOfStockItemNames { get; set; } = new();
        public List<string> InStockItemNames { get; set; } = new();

        // Supplies — movement WITHIN the selected period, from supply_stock_logs 
        public int PiecesRestocked { get; set; }
        public int PiecesUsed { get; set; }

        // Short takeaway for the Supplies card, e.g.
        // "Most used: Gauze Pads — 42 used".
        public string SuppliesInsight { get; set; } = string.Empty;

        // Services Rendered
        public int TotalServicesRendered { get; set; }
        public Dictionary<string, int> ServiceBreakdown { get; set; } = new();
    }

    public enum ReportPeriod
    {
        Daily,
        Weekly,
        Monthly,
        Custom
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

    // One point/segment/bar for a Syncfusion chart series.
    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    // One row of the Weekly appointments table: Day | Completed | Cancelled.
    public class AppointmentDayRow
    {
        public DateTime Date { get; set; }
        public string DayLabel { get; set; } = string.Empty;
        public int Completed { get; set; }
        public int Cancelled { get; set; }
    }

    // One row of the Monthly appointments table: Day | Completed | Cancelled,
    public class AppointmentWeekdayRow
    {
        public string DayLabel { get; set; } = string.Empty;
        public int Completed { get; set; }
        public int Cancelled { get; set; }
    }

    // One row of the Today appointment list (Daily tab) 
    public class TodayAppointmentRow
    {
        // Not displayed — used only to sort the combined list by time.
        public DateTime SortTime { get; set; }
        public string TimeLabel { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public Color StatusColor { get; set; } = Colors.Gray;
        public Color StatusBgColor { get; set; } = Colors.LightGray;
    }

    // One row of the Treatments table: Treatment | Count | % of Total.
    public class TreatmentRow
    {
        public string Treatment { get; set; } = string.Empty;
        public int Count { get; set; }
        public double PercentOfTotal { get; set; }
    }

    // One row of the "Top Services by Revenue" table: Service | Times
    // Billed | Revenue. Top 5 only, ranked by Revenue descending.
    public class TopServiceRow
    {
        public string Service { get; set; } = string.Empty;
        public int TimesBilled { get; set; }
        public decimal Revenue { get; set; }
    }

    // One row of the Supplies table: Name | Used | Restocked. Ranked by
    // Used descending; only items with movement in the period are shown.
    public class SupplyUsageRow
    {
        public string Name { get; set; } = string.Empty;
        public int Used { get; set; }
        public int Restocked { get; set; }
    }
}

using ClinicApp.Models.ReportModels;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicApp.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        readonly SupabaseDataService dataService;

        [ObservableProperty] private ReportPeriod selectedPeriod = ReportPeriod.Daily;
        [ObservableProperty] private ReportsSummary? currentReport;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string dateRangeLabel = string.Empty;

        public ReportsViewModel(SupabaseDataService dataService)
        {
            this.dataService = dataService;
        }

        public void OnAppearing() => _ = LoadReport();

        [RelayCommand]
        async Task SetPeriod(string period)
        {
            SelectedPeriod = period switch
            {
                "Weekly" => ReportPeriod.Weekly,
                "Monthly" => ReportPeriod.Monthly,
                _ => ReportPeriod.Daily
            };
            await LoadReport();
        }

        [RelayCommand]
        async Task LoadReport()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {

                var (start, end, label) = GetDateRange(SelectedPeriod);
                var report = new ReportsSummary { PeriodLabel = label, StartDate = start, EndDate = end };
                DateRangeLabel = label;

                // ── APPOINTMENTS (bookings table) ──
                var bookings = await dataService.GetAllBookingsForReportAsync(start, end);
                report.TotalAppointments = bookings.Count;
                report.CompletedAppointments = bookings.Count(b => b.Status == "completed");
                report.PendingAppointments = bookings.Count(b => b.Status == "pending");
                report.CancelledAppointments = bookings.Count(b => b.Status == "cancelled" || b.Status == "rejected");

                // ── TREATMENTS (treatment_history table — tooth-condition based, not service-based) ──
                var treatments = await dataService.GetAllTreatmentHistoryForReportAsync(start, end);
                report.TotalTreatments = treatments.Count;
                report.TreatmentBreakdown = treatments
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.Condition) ? "Unspecified" : t.Condition)
                    .ToDictionary(g => g.Key, g => g.Count());

                // ── BILLING (bills table — the real one) ──
                var allBills = await dataService.GetAllBillsAsync();
                var billsInRange = allBills.Where(b => b.VisitDate >= start && b.VisitDate < end).ToList();
                report.TotalRevenue = billsInRange.Sum(b => b.AmountPaid);
                report.OutstandingBalance = billsInRange.Sum(b => b.Balance);

                // ── SERVICES RENDERED (bill_items, joined to bills in this date range) ──
                var billIdsInRange = billsInRange.Select(b => b.Id).ToHashSet();
                var allBillItems = await dataService.GetAllBillItemsAsync();
                var itemsInRange = allBillItems.Where(i => billIdsInRange.Contains(i.BillId)).ToList();

                report.TotalServicesRendered = itemsInRange.Count;
                report.ServiceBreakdown = itemsInRange
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.ServiceName) ? "Unspecified" : i.ServiceName)
                    .ToDictionary(g => g.Key, g => g.Count());

                // ── SUPPLIES (low stock, current snapshot — uses the model's own IsLowStock) ──
                var supplies = await dataService.GetSuppliesAsync();
                var lowStock = supplies.Where(s => s.IsLowStock).ToList();
                report.LowStockItemCount = lowStock.Count;
                report.LowStockItemNames = lowStock.Select(s => s.Name).ToList();

                CurrentReport = report;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportsViewModel] LoadReport error: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Failed to load report.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        static (DateTime start, DateTime end, string label) GetDateRange(ReportPeriod period)
        {
            var today = DateTime.Today;
            return period switch
            {
                ReportPeriod.Weekly =>
                    (today.AddDays(-(int)today.DayOfWeek), today.AddDays(7 - (int)today.DayOfWeek), "This Week"),
                ReportPeriod.Monthly =>
                    (new DateTime(today.Year, today.Month, 1),
                     new DateTime(today.Year, today.Month, 1).AddMonths(1),
                     today.ToString("MMMM yyyy")),
                _ => (today, today.AddDays(1), today.ToString("MMMM d, yyyy"))
            };
        }
    }
}
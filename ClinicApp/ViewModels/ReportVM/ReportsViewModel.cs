using ClinicApp.Models.ReportModels;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views.SupplyRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    // ── HOW THIS PAGE WORKS ──────────────────────────────────────────
    // 1. Three tabs (Daily/Weekly/Monthly) pick a GRANULARITY, not a
    //    single fixed period. Tapping a tab calls SetPeriod, which
    //    rebuilds the dropdown (DateOptions) for that granularity —
    //    e.g. Daily rebuilds to "the last 30 days" (Sundays skipped,
    //    clinic is closed), Weekly to "the last 4 weeks," Monthly to
    //    "the last 3 months."
    // 2. The dropdown is capped to how far back real data actually
    //    goes (EnsureEarliestDateAsync), so a brand-new clinic sees a
    //    short dropdown instead of a long one full of empty periods.
    // 3. Picking a dropdown entry (SelectedDateOption) triggers
    //    LoadReport, which pulls all 5 report sections for that exact
    //    date range and fills 4 ObservableCollection<ChartDataPoint>
    //    properties — the page's Syncfusion chart series bind directly
    //    to these via ItemsSource/XBindingPath/YBindingPath.
    // 4. Billing chart granularity depends on the tab:
    //    - Weekly/Monthly: breaks the SELECTED week/month down into
    //      daily buckets (7 days, or ~28-31 days) — a zoom INTO that
    //      one period, not a trend across separate periods.
    //    - Daily: bills only store a date (no time-of-day) so true
    //      hourly bars aren't possible yet — stays as a 7-day trend
    //      across the dropdown until that data exists.
    // 5. Each report card's stat area is tappable — confirms with the
    //    user, then deep-links to the relevant list page.
    // ───────────────────────────────────────────────────────────────
    public partial class ReportsViewModel : ObservableObject
    {
        readonly SupabaseDataService dataService;

        // Earliest date any real data exists (earliest booking or bill).
        // Computed once and cached — see EnsureEarliestDateAsync.
        DateTime? _earliestDataDate;

        [ObservableProperty] private ReportPeriod selectedPeriod = ReportPeriod.Daily;
        [ObservableProperty] private ReportsSummary? currentReport;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string dateRangeLabel = string.Empty;

        public ObservableCollection<DateRangeOption> DateOptions { get; } = new();

        [ObservableProperty] private DateRangeOption? selectedDateOption;

        // Chart data — the page's Syncfusion series bind straight to
        // these (ItemsSource="{Binding AppointmentChartData}" etc.).
        // Cleared and rebuilt every LoadReport call.
        public ObservableCollection<ChartDataPoint> AppointmentChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> SupplyChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> BillingChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> TreatmentChartData { get; } = new();

        public ReportsViewModel(SupabaseDataService dataService)
        {
            this.dataService = dataService;
        }

        public void OnAppearing() => _ = SetPeriod("Daily");

        // Tapping a Daily/Weekly/Monthly tab lands here. Rebuilds the
        // dropdown for that granularity and auto-selects the most
        // recent entry, which triggers LoadReport via
        // OnSelectedDateOptionChanged below.
        [RelayCommand]
        async Task SetPeriod(string period)
        {
            SelectedPeriod = period switch
            {
                "Weekly" => ReportPeriod.Weekly,
                "Monthly" => ReportPeriod.Monthly,
                _ => ReportPeriod.Daily
            };

            await EnsureEarliestDateAsync();

            DateOptions.Clear();
            foreach (var opt in BuildDateOptions(SelectedPeriod))
                DateOptions.Add(opt);

            // Setting this always re-triggers a load since it's a fresh
            // object each rebuild.
            SelectedDateOption = DateOptions.FirstOrDefault();
        }

        partial void OnSelectedDateOptionChanged(DateRangeOption? value)
        {
            if (value != null) _ = LoadReport();
        }

        // Finds the earliest booking/bill date once and caches it, so
        // every dropdown rebuild after the first doesn't re-fetch. This
        // is what makes the dropdown "shrink" for a new clinic instead
        // of always showing a fixed 30/4/3 count.
        async Task EnsureEarliestDateAsync()
        {
            if (_earliestDataDate.HasValue) return;

            try
            {
                var allBookings = await dataService.GetAllBookingsForReportAsync(DateTime.MinValue, DateTime.MaxValue);
                var allBills = await dataService.GetAllBillsAsync();

                var earliestBooking = allBookings.Count > 0 ? allBookings.Min(b => b.AppointmentDate) : (DateTime?)null;
                var earliestBill = allBills.Count > 0 ? allBills.Min(b => b.CreatedAt) : (DateTime?)null;

                _earliestDataDate = new[] { earliestBooking, earliestBill }
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .DefaultIfEmpty(DateTime.Today)
                    .Min();
            }
            catch
            {
                // If this fails for any reason, fall back to "today" —
                // worst case the dropdown just shows one entry instead
                // of crashing the page.
                _earliestDataDate = DateTime.Today;
            }
        }

        // Builds the dropdown entries for the given granularity, newest
        // first, stopping once it reaches earlier than any real data.
        List<DateRangeOption> BuildDateOptions(ReportPeriod period)
        {
            var today = DateTime.Today;
            var earliest = _earliestDataDate ?? today;
            var options = new List<DateRangeOption>();

            switch (period)
            {
                case ReportPeriod.Daily:
                    // Clinic is closed Sundays — skip them so the
                    // dropdown never offers a day that's guaranteed
                    // to show an empty report. Still counts back 30
                    // calendar days; Sundays just aren't added.
                    for (int i = 0; i < 30; i++)
                    {
                        var day = today.AddDays(-i);
                        if (day < earliest.Date) break;
                        if (day.DayOfWeek == DayOfWeek.Sunday) continue;

                        options.Add(new DateRangeOption
                        {
                            Start = day,
                            End = day.AddDays(1),
                            Label = i == 0 ? "Today" : i == 1 ? "Yesterday" : day.ToString("MMM dd")
                        });
                    }
                    break;

                case ReportPeriod.Weekly:
                    for (int i = 0; i < 4; i++)
                    {
                        var weekStart = today.AddDays(-(int)today.DayOfWeek).AddDays(-7 * i);
                        var weekEnd = weekStart.AddDays(7);
                        if (weekEnd <= earliest.Date) break;

                        options.Add(new DateRangeOption
                        {
                            Start = weekStart,
                            End = weekEnd,
                            Label = i == 0 ? "This Week" : $"{weekStart:MMM d} - {weekEnd.AddDays(-1):MMM d}"
                        });
                    }
                    break;

                case ReportPeriod.Monthly:
                    for (int i = 0; i < 3; i++)
                    {
                        var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                        var monthEnd = monthStart.AddMonths(1);
                        if (monthEnd <= earliest.Date) break;

                        options.Add(new DateRangeOption
                        {
                            Start = monthStart,
                            End = monthEnd,
                            Label = i == 0 ? "This Month" : monthStart.ToString("MMM yyyy")
                        });
                    }
                    break;
            }

            // A brand-new clinic with zero data yet would otherwise get
            // an empty dropdown — always keep at least the current
            // period so there's something to select.
            if (options.Count == 0)
            {
                options.Add(period switch
                {
                    ReportPeriod.Weekly => new DateRangeOption
                    {
                        Start = today.AddDays(-(int)today.DayOfWeek),
                        End = today.AddDays(7 - (int)today.DayOfWeek),
                        Label = "This Week"
                    },
                    ReportPeriod.Monthly => new DateRangeOption
                    {
                        Start = new DateTime(today.Year, today.Month, 1),
                        End = new DateTime(today.Year, today.Month, 1).AddMonths(1),
                        Label = today.ToString("MMM yyyy")
                    },
                    _ => new DateRangeOption { Start = today, End = today.AddDays(1), Label = "Today" }
                });
            }

            return options;
        }

        [RelayCommand]
        async Task LoadReport()
        {
            if (IsBusy || SelectedDateOption == null) return;
            IsBusy = true;

            try
            {
                var start = SelectedDateOption.Start;
                var end = SelectedDateOption.End;
                var label = SelectedDateOption.Label;

                var report = new ReportsSummary { PeriodLabel = label, StartDate = start, EndDate = end };
                DateRangeLabel = label;

                // ── APPOINTMENTS ──
                var bookings = await dataService.GetAllBookingsForReportAsync(start, end);
                report.TotalAppointments = bookings.Count;
                report.CompletedAppointments = bookings.Count(b => b.Status == "completed");
                report.CancelledAppointments = bookings.Count(b => b.Status == "cancelled" || b.Status == "rejected");

                // Pending is a catch-all (Total minus the two known
                // buckets) instead of an exact "pending" string match,
                // so bookings sitting in other statuses (e.g. "approved")
                // still get counted instead of vanishing from the chart.
                report.PendingAppointments = report.TotalAppointments
                    - report.CompletedAppointments
                    - report.CancelledAppointments;

                AppointmentChartData.Clear();
                AppointmentChartData.Add(new ChartDataPoint { Label = "Completed", Value = report.CompletedAppointments });
                AppointmentChartData.Add(new ChartDataPoint { Label = "Pending", Value = report.PendingAppointments });
                AppointmentChartData.Add(new ChartDataPoint { Label = "Cancelled", Value = report.CancelledAppointments });

                // ── TREATMENTS (condition-based — see note below) ──
                var treatments = await dataService.GetAllTreatmentHistoryForReportAsync(start, end);
                report.TotalTreatments = treatments.Count;
                report.TreatmentBreakdown = treatments
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.Condition) ? "Unspecified" : t.Condition)
                    .ToDictionary(g => g.Key, g => g.Count());
                report.MostCommonTreatment = report.TreatmentBreakdown.Count > 0
                    ? report.TreatmentBreakdown.OrderByDescending(kv => kv.Value).First().Key
                    : "—";

                TreatmentChartData.Clear();
                foreach (var kv in report.TreatmentBreakdown.OrderByDescending(kv => kv.Value))
                    TreatmentChartData.Add(new ChartDataPoint { Label = kv.Key, Value = kv.Value });

                // ── BILLING ──
                var allBills = await dataService.GetAllBillsAsync();
                var billsInRange = allBills.Where(b => b.VisitDate >= start && b.VisitDate < end).ToList();
                report.TotalRevenue = billsInRange.Sum(b => b.AmountPaid);
                report.OutstandingBalance = billsInRange.Sum(b => b.Balance);

                BillingChartData.Clear();

                if (SelectedPeriod == ReportPeriod.Daily)
                {
                    // Bills only store a date, no time-of-day, so true
                    // hourly bars aren't possible yet. Stays as a 7-day
                    // trend across the dropdown (newest-first, so grab
                    // from the front then reverse for left-to-right).
                    foreach (var opt in DateOptions.Take(7).Reverse())
                    {
                        var revenue = (double)allBills
                            .Where(b => b.VisitDate >= opt.Start && b.VisitDate < opt.End)
                            .Sum(b => b.AmountPaid);
                        BillingChartData.Add(new ChartDataPoint { Label = opt.Label, Value = revenue });
                    }
                }
                else
                {
                    // Weekly/Monthly: zoom INTO the selected period —
                    // one bar per day inside it (7 for a week, ~28-31
                    // for a month) — instead of a trend across separate
                    // weeks/months.
                    for (var day = start.Date; day < end.Date; day = day.AddDays(1))
                    {
                        var dayEnd = day.AddDays(1);
                        var revenue = (double)allBills
                            .Where(b => b.VisitDate >= day && b.VisitDate < dayEnd)
                            .Sum(b => b.AmountPaid);
                        BillingChartData.Add(new ChartDataPoint { Label = day.ToString("MMM d"), Value = revenue });
                    }
                }

                // ── SERVICES RENDERED ──
                var billIdsInRange = billsInRange.Select(b => b.Id).ToHashSet();
                var allBillItems = await dataService.GetAllBillItemsAsync();
                var itemsInRange = allBillItems.Where(i => billIdsInRange.Contains(i.BillId)).ToList();

                report.TotalServicesRendered = itemsInRange.Count;
                report.ServiceBreakdown = itemsInRange
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.ServiceName) ? "Unspecified" : i.ServiceName)
                    .ToDictionary(g => g.Key, g => g.Count());

                // ── SUPPLIES — overview (current snapshot, not period-based) ──
                var supplies = await dataService.GetSuppliesAsync();
                report.TotalSupplies = supplies.Count;
                report.OutOfStockCount = supplies.Count(s => s.IsOutOfStock);
                report.LowStockItemCount = supplies.Count(s => s.IsLowStock && !s.IsOutOfStock);
                report.InStockCount = report.TotalSupplies - report.LowStockItemCount - report.OutOfStockCount;
                report.LowStockItemNames = supplies
                    .Where(s => s.IsLowStock && !s.IsOutOfStock)
                    .Select(s => s.Name)
                    .ToList();

                SupplyChartData.Clear();
                SupplyChartData.Add(new ChartDataPoint { Label = "In Stock", Value = report.InStockCount });
                SupplyChartData.Add(new ChartDataPoint { Label = "Low Stock", Value = report.LowStockItemCount });
                SupplyChartData.Add(new ChartDataPoint { Label = "Out of Stock", Value = report.OutOfStockCount });

                // ── SUPPLIES — movement WITHIN this period (supply_stock_logs) ──
                var logs = await dataService.GetAllStockLogsForReportAsync(start, end);
                report.PiecesRestocked = logs.Where(l => l.ChangeInPieces > 0).Sum(l => l.ChangeInPieces);
                report.PiecesUsed = Math.Abs(logs.Where(l => l.ChangeInPieces < 0).Sum(l => l.ChangeInPieces));

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

        // ── Tap-to-confirm-then-navigate, one per card ──────────────
        // Each stat area shows what's relevant, asks the user to
        // confirm, then deep-links via Shell route (all confirmed
        // registered in AppShell.cs).

        [RelayCommand]
        async Task ViewAppointments()
        {
            if (CurrentReport == null) return;

            bool go = await Shell.Current.DisplayAlert(
                "Appointments",
                $"{CurrentReport.TotalAppointments} appointment(s) this period " +
                $"({CurrentReport.CompletedAppointments} done, {CurrentReport.PendingAppointments} pending, " +
                $"{CurrentReport.CancelledAppointments} cancelled).\n\nView the full appointments list?",
                "View", "Cancel");

            if (go) await Shell.Current.GoToAsync(nameof(AppointmentPage));
        }

        [RelayCommand]
        async Task ViewBilling()
        {
            if (CurrentReport == null) return;

            bool go = await Shell.Current.DisplayAlert(
                "Billing",
                $"₱{CurrentReport.TotalRevenue:N2} collected, ₱{CurrentReport.OutstandingBalance:N2} outstanding this period.\n\n" +
                "View the full transaction history?",
                "View", "Cancel");

            if (go) await Shell.Current.GoToAsync(nameof(TransactionPage));
        }

        [RelayCommand]
        async Task ViewSupplies()
        {
            if (CurrentReport == null) return;

            var message = $"{CurrentReport.TotalSupplies} total supply item(s): " +
                           $"{CurrentReport.InStockCount} in stock, {CurrentReport.LowStockItemCount} low, " +
                           $"{CurrentReport.OutOfStockCount} out of stock.";

            if (CurrentReport.LowStockItemNames.Count > 0)
                message += $"\n\nLow on: {string.Join(", ", CurrentReport.LowStockItemNames)}";

            message += "\n\nView the full supply list?";

            bool go = await Shell.Current.DisplayAlert("Medical Supplies", message, "View", "Cancel");

            if (go) await Shell.Current.GoToAsync(nameof(SupplyListPage));
        }

        // TODO: Treatment tap — no general "all treatments" page is
        // registered in AppShell yet (TreatmentHistoryPage needs a
        // specific patient id). Add a command here once we know where
        // this should navigate.
    }
}

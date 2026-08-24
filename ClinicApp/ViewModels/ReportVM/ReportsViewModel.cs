using ClinicApp.Drawables;
using ClinicApp.Models.ReportModels;
using ClinicApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    // ── HOW THIS PAGE WORKS ──────────────────────────────────────────
    // 1. Three tabs (Daily/Weekly/Monthly) pick a GRANULARITY, not a
    //    single fixed period. Tapping a tab calls SetPeriod, which
    //    rebuilds the dropdown (DateOptions) for that granularity —
    //    e.g. Daily rebuilds to "the last 30 days," Weekly to "the
    //    last 4 weeks," Monthly to "the last 3 months."
    // 2. The dropdown is capped to how far back real data actually
    //    goes (EnsureEarliestDateAsync), so a brand-new clinic sees a
    //    short dropdown instead of a long one full of empty periods.
    // 3. Picking a dropdown entry (SelectedDateOption) triggers
    //    LoadReport, which pulls all 5 report sections for that exact
    //    date range and builds the chart drawables the page displays.
    // 4. All charts are hand-drawn (DonutChartDrawable/LineChartDrawable,
    //    plain Microsoft.Maui.Graphics — no chart package) rather than
    //    a third-party library, so every chart shares the app's exact
    //    colors/style. The page binds each one straight to a
    //    GraphicsView's Drawable property.
    // 5. The Billing line chart is the one exception to "just the
    //    selected range" — it plots every entry currently in the
    //    dropdown (not only the selected one), so there's an actual
    //    trend to look at rather than a single point.
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

        // Chart drawables the page's GraphicsView controls bind to
        // directly (Drawable="{Binding AppointmentChart}" etc.). Built
        // fresh in LoadReport every time the period changes.
        [ObservableProperty] private DonutChartDrawable? appointmentChart;
        [ObservableProperty] private DonutChartDrawable? supplyChart;
        [ObservableProperty] private LineChartDrawable? billingChart;

        // Treatments has no donut/line shape — this collection drives a
        // plain CollectionView of proportionally-widthed bars instead.
        // See ReportsPage.xaml.
        public ObservableCollection<TreatmentBarItem> TreatmentBars { get; } = new();

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
                    for (int i = 0; i < 30; i++)
                    {
                        var day = today.AddDays(-i);
                        if (day < earliest.Date) break;

                        options.Add(new DateRangeOption
                        {
                            Start = day,
                            End = day.AddDays(1),
                            Label = i == 0 ? "Today" : i == 1 ? "Yesterday" : day.ToString("MMM dd, yyyy")
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
                            Label = i == 0 ? "This Month" : monthStart.ToString("MMMM yyyy")
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
                        Label = today.ToString("MMMM yyyy")
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
                report.PendingAppointments = bookings.Count(b => b.Status == "pending");
                report.CancelledAppointments = bookings.Count(b => b.Status == "cancelled" || b.Status == "rejected");

                AppointmentChart = new DonutChartDrawable
                {
                    Segments = new()
                    {
                        (report.CompletedAppointments, Color.FromArgb("#2E7D32")),
                        (report.PendingAppointments,   Color.FromArgb("#F57C00")),
                        (report.CancelledAppointments, Color.FromArgb("#D32F2F")),
                    }
                };

                // ── TREATMENTS (condition-based — see note below) ──
                var treatments = await dataService.GetAllTreatmentHistoryForReportAsync(start, end);
                report.TotalTreatments = treatments.Count;
                report.TreatmentBreakdown = treatments
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.Condition) ? "Unspecified" : t.Condition)
                    .ToDictionary(g => g.Key, g => g.Count());
                report.MostCommonTreatment = report.TreatmentBreakdown.Count > 0
                    ? report.TreatmentBreakdown.OrderByDescending(kv => kv.Value).First().Key
                    : "—";

                TreatmentBars.Clear();
                var maxCount = report.TreatmentBreakdown.Count > 0 ? report.TreatmentBreakdown.Values.Max() : 0;
                foreach (var kv in report.TreatmentBreakdown.OrderByDescending(kv => kv.Value))
                {
                    TreatmentBars.Add(new TreatmentBarItem
                    {
                        Label = kv.Key,
                        Count = kv.Value,
                        WidthProportion = maxCount > 0 ? (double)kv.Value / maxCount : 0
                    });
                }

                // ── BILLING ──
                var allBills = await dataService.GetAllBillsAsync();
                var billsInRange = allBills.Where(b => b.VisitDate >= start && b.VisitDate < end).ToList();
                report.TotalRevenue = billsInRange.Sum(b => b.AmountPaid);
                report.OutstandingBalance = billsInRange.Sum(b => b.Balance);

                // Line chart plots the WHOLE dropdown's trend, not just
                // the selected point — a 1-point line isn't useful.
                // DateOptions is newest-first, so reverse for a
                // left-to-right oldest→newest reading.
                var billingValues = DateOptions
                    .Select(opt => (float)allBills
                        .Where(b => b.VisitDate >= opt.Start && b.VisitDate < opt.End)
                        .Sum(b => b.AmountPaid))
                    .Reverse()
                    .ToList();

                BillingChart = new LineChartDrawable
                {
                    Values = billingValues,
                    LineColor = Color.FromArgb("#2E7D32")
                };

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

                SupplyChart = new DonutChartDrawable
                {
                    Segments = new()
                    {
                        (report.InStockCount,      Color.FromArgb("#2E7D32")),
                        (report.LowStockItemCount, Color.FromArgb("#F57C00")),
                        (report.OutOfStockCount,   Color.FromArgb("#D32F2F")),
                    }
                };

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
    }
}

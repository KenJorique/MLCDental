using ClinicApp.Models.ReportModels;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.SupplyRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        readonly SupabaseDataService dataService;

        // Clinic operating days, Monday–Saturday 
        static readonly DayOfWeek[] ClinicWeekdays =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
        };

        // Earliest date any real data exists (earliest booking or bill).
        // Computed once and cached — see EnsureEarliestDateAsync.
        DateTime? _earliestDataDate;

        [ObservableProperty] private ReportPeriod selectedPeriod = ReportPeriod.Daily;
        [ObservableProperty] private ReportsSummary? currentReport;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string dateRangeLabel = string.Empty;

        // Billing chart X-axis title. Always "Date" now — the old "Hour" value only applied to Daily's
        // hourly chart, which was removed since it's hidden on Daily anyway (see ShowBillingChart below).
        [ObservableProperty] private string billingAxisTitle = "Date";

        // Whether the Billing line chart shows a number label on every point. Off for Monthly (too many points, labels overlap the line) — on for Daily/Weekly.
        [ObservableProperty] private bool showBillingDataLabels = true;

        // Whether the Billing line chart shows at all. Off for Daily: 
        [ObservableProperty] private bool showBillingChart = true;

        // Treatments chart height, sized to the number of treatment categories so Daily/Weekly stay compact while Monthly (usually more categories) gets room to breathe. Set in LoadReport.
        [ObservableProperty] private double treatmentsChartHeight = 240;

        // ── Which appointments view to show under the chart. Exactly one of these three is true at a time — set in SetPeriod. ──
        [ObservableProperty] private bool showDailyAppointmentList = true;
        [ObservableProperty] private bool showWeeklyAppointmentTable;
        [ObservableProperty] private bool showMonthlyAppointmentTable;

        // Whether a Custom range is currently applied — swaps the date-row from the Daily/Weekly/Monthly Picker
        // to a tappable label showing the picked range (tapping it reopens the range sheet to pick a different one).
        [ObservableProperty] private bool isCustomPeriodActive;

        public ObservableCollection<DateRangeOption> DateOptions { get; } = new();

        [ObservableProperty] private DateRangeOption? selectedDateOption;

        // Chart data — the page's Syncfusion series bind straight to
        // these (ItemsSource="{Binding AppointmentChartData}" etc.).
        // Cleared and rebuilt every LoadReport call.|

        public ObservableCollection<ChartDataPoint> AppointmentChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> SupplyChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> BillingChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> TreatmentChartData { get; } = new();

        // ── Table-row collections backing the CollectionViews added under each chart. Cleared and rebuilt every LoadReport call, same as the chart data above. ──
        public ObservableCollection<TodayAppointmentRow> TodayAppointmentRows { get; } = new();
        public ObservableCollection<AppointmentDayRow> AppointmentWeeklyRows { get; } = new();
        public ObservableCollection<AppointmentWeekdayRow> AppointmentMonthlyRows { get; } = new();
        public ObservableCollection<TreatmentRow> TreatmentTableRows { get; } = new();
        public ObservableCollection<TopServiceRow> TopServiceRows { get; } = new();
        public ObservableCollection<SupplyUsageRow> SupplyUsageRows { get; } = new();

        // Injects the shared data service used for every Supabase call on this page.
        public ReportsViewModel(SupabaseDataService dataService)
        {
            this.dataService = dataService;
        }

        // Runs when the page appears; loads the Daily tab by default.
        public void OnAppearing() => _ = SetPeriod("Daily");

        // Tapping a Daily/Weekly/Monthly tab lands here. Rebuilds the
        // dropdown for that granularity 
        [RelayCommand]
        async Task SetPeriod(string period)
        {
            SelectedPeriod = period switch
            {
                "Weekly" => ReportPeriod.Weekly,
                "Monthly" => ReportPeriod.Monthly,
                _ => ReportPeriod.Daily
            };
            IsCustomPeriodActive = false;

            ApplyGroupingFlags(SelectedPeriod);

            await EnsureEarliestDateAsync();

            DateOptions.Clear();
            foreach (var opt in BuildDateOptions(SelectedPeriod))
                DateOptions.Add(opt);

            // Setting this always re-triggers a load since it's a fresh
            // object each rebuild.
            SelectedDateOption = DateOptions.FirstOrDefault();
        }

        // Called from the Custom range bottom sheet (CustomDateRangeSheet) after the user taps Apply.
        // Unlike SetPeriod, the "shape" here (day list vs day table vs weekday table) isn't known from
        // a tab name — it's inferred from how long the picked range is, via GetEffectiveGrouping below.
        public async Task ApplyCustomRange(DateTime start, DateTime end)
        {
            if (end < start) (start, end) = (end, start); // guard against a reversed pick

            SelectedPeriod = ReportPeriod.Custom;
            IsCustomPeriodActive = true;
            var grouping = GetEffectiveGrouping(SelectedPeriod, start, end);
            ApplyGroupingFlags(grouping);

            var option = new DateRangeOption
            {
                Start = start.Date,
                End = end.Date.AddDays(1), // exclusive end, matching every other DateRangeOption in this file
                Label = BuildCustomRangeLabel(start, end)
            };

            await EnsureEarliestDateAsync();

            // Custom mode only ever has the one range the user just picked — no preset list to browse.
            DateOptions.Clear();
            DateOptions.Add(option);
            SelectedDateOption = option; // triggers LoadReport via OnSelectedDateOptionChanged
        }

        // Sets which appointments view and billing chart config to use, for the given GROUPING shape
        // (Daily/Weekly/Monthly). Called with SelectedPeriod directly for the three standard tabs, and
        // with the span-inferred grouping for a Custom range (see GetEffectiveGrouping).
        void ApplyGroupingFlags(ReportPeriod grouping)
        {
            // Monthly has too many points for on-chart labels to stay readable, so those are hidden there (values are still visible via tooltip in the chart).
            ShowBillingDataLabels = grouping != ReportPeriod.Monthly;
            ShowBillingChart = grouping != ReportPeriod.Daily;

            ShowDailyAppointmentList = grouping == ReportPeriod.Daily;
            ShowWeeklyAppointmentTable = grouping == ReportPeriod.Weekly;
            ShowMonthlyAppointmentTable = grouping == ReportPeriod.Monthly;
        }

        // For Daily/Weekly/Monthly this is just the period itself. For Custom, the appointments-table
        // shape is inferred from the picked range's length: same-day acts like Daily, up to a week
        // uses the day-by-day table (Weekly's shape), anything longer uses the weekday-aggregate table
        // (Monthly's shape) — a day-by-day table past about a week stops being readable on mobile.
        static ReportPeriod GetEffectiveGrouping(ReportPeriod period, DateTime start, DateTime end)
        {
            if (period != ReportPeriod.Custom) return period;

            var spanDays = (end.Date - start.Date).Days;
            if (spanDays <= 0) return ReportPeriod.Daily;
            if (spanDays <= 7) return ReportPeriod.Weekly;
            return ReportPeriod.Monthly;
        }

        // Builds the dropdown label for a custom range, e.g. "Aug 17 - Aug 21, 2026 (5 days)".
        // Inclusive calendar-day count (both endpoints counted) — matches the actual span of data
        // covered, and matches how most people read "Aug 17 to Aug 21" (5 calendar days).
        static string BuildCustomRangeLabel(DateTime start, DateTime end)
        {
            var days = (end.Date - start.Date).Days + 1;
            var range = start.Year == end.Year
                ? $"{start:MMM d} - {end:MMM d, yyyy}"
                : $"{start:MMM d, yyyy} - {end:MMM d, yyyy}";
            return $"{range} ({days} day{(days == 1 ? "" : "s")})";
        }

        // Fires when the dropdown selection changes; triggers a fresh report load for that range.
        partial void OnSelectedDateOptionChanged(DateRangeOption? value)
        {
            if (value != null) _ = LoadReport();
        }

        // Finds the earliest booking/bill date once and caches it
        async Task EnsureEarliestDateAsync()
        {
            if (_earliestDataDate.HasValue) return;

            try
            {
                var allBookings = await dataService.GetAllBookingsForReportAsync(DateTime.MinValue, DateTime.MaxValue);
                var allEntries = await dataService.GetAllAppointmentEntriesForReportAsync(DateTime.MinValue, DateTime.MaxValue);
                var allBills = await dataService.GetAllBillsAsync();

                var earliestBooking = allBookings.Count > 0 ? allBookings.Min(b => b.AppointmentDate) : (DateTime?)null;
                var earliestEntry = allEntries.Count > 0 ? allEntries.Min(e => e.AppointmentDateTime) : (DateTime?)null;
                var earliestBill = allBills.Count > 0 ? allBills.Min(b => b.CreatedAt) : (DateTime?)null;

                _earliestDataDate = new[] { earliestBooking, earliestEntry, earliestBill }
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .DefaultIfEmpty(DateTime.Today)
                    .Min();
            }
            catch
            {
                // If this fails for any reason, fall back to "today" —
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

        // Same Completed/Cancelled classification the period-level
        // totals use above (LoadReport), just narrowed to one calendar
        // day. Shared by the Weekly table and the Monthly weekday
        // aggregation, so both always agree with each other and with
        // the top-level totals.
        static (int Completed, int Cancelled) GetDayAppointmentCounts(
            DateTime day,
            List<Models.SupabaseModels.SupabaseBill> billsInRange,
            List<Models.SupabaseModels.SupabaseCancelledAppointment> cancelledLogs,
            List<Models.SupabaseModels.SupabaseAppointmentEntry> entries,
            DateTime today)
        {
            var dayEnd = day.AddDays(1);

            int completed = billsInRange.Count(b => b.VisitDate >= day && b.VisitDate < dayEnd);
            int explicitCancelled = cancelledLogs.Count(c => c.AppointmentDateTime >= day && c.AppointmentDateTime < dayEnd);

            // No-show only applies once the day is fully over — an
            // entry left on a day still in progress or in the future
            // is Pending, not a no-show.
            int noShow = day.Date < today
                ? entries.Count(e => e.AppointmentDateTime >= day && e.AppointmentDateTime < dayEnd)
                : 0;

            return (completed, explicitCancelled + noShow);
        }

        // Builds the "Busiest day: X — N completed" insight. Lists
        static string BuildBusiestDayInsight(IEnumerable<(string Label, int Completed)> rows, string noneMessage)
        {
            var list = rows.ToList();
            int maxCompleted = list.Count > 0 ? list.Max(r => r.Completed) : 0;
            if (maxCompleted <= 0) return noneMessage;

            var tiedNames = list.Where(r => r.Completed == maxCompleted).Select(r => r.Label).ToList();

            string names = tiedNames.Count switch
            {
                1 => tiedNames[0],
                2 => $"{tiedNames[0]} and {tiedNames[1]}",
                _ => $"{string.Join(", ", tiedNames.Take(tiedNames.Count - 1))} and {tiedNames[^1]}"
            };

            string suffix = tiedNames.Count > 1
                ? $" — {maxCompleted} completed each"
                : $" — {maxCompleted} completed";

            return $"Busiest day: {names}{suffix}";
        }

        // Builds the "Most common: X — N%" insight. 
        static string BuildMostCommonTreatmentInsight(IReadOnlyList<TreatmentRow> rows)
        {
            if (rows.Count == 0) return "No treatments logged yet this period.";

            int maxCount = rows.Max(r => r.Count);
            var tied = rows.Where(r => r.Count == maxCount).ToList();
            double pct = tied[0].PercentOfTotal;

            string names = tied.Count switch
            {
                1 => tied[0].Treatment,
                2 => $"{tied[0].Treatment} and {tied[1].Treatment}",
                _ => $"{string.Join(", ", tied.Take(tied.Count - 1).Select(t => t.Treatment))} and {tied[^1].Treatment}"
            };

            string suffix = tied.Count > 1 ? $" — {pct:N0}% each" : $" — {pct:N0}%";
            return $"Most common: {names}{suffix}";
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

                // Which table SHAPE to render below — for Daily/Weekly/Monthly this is just SelectedPeriod;
                // for Custom it's inferred from how long the picked range is (see GetEffectiveGrouping).
                var grouping = GetEffectiveGrouping(SelectedPeriod, start, end.AddDays(-1)); // end is exclusive here, so step back one day before measuring the span

                // ── BILLING (fetched early — Completed appointments below need it) ──
                var allBills = await dataService.GetAllBillsAsync();
                var billsInRange = allBills.Where(b => b.VisitDate >= start && b.VisitDate < end).ToList();

                // ── APPOINTMENTS ──
                var entries = await dataService.GetAllAppointmentEntriesForReportAsync(start, end);
                var today = DateTime.Today;

                report.CompletedAppointments = billsInRange.Count; // a bill existing for this date IS the "this visit happened" signal

                // Explicit cancellations — CancelAppointment() logs one row here 
                var cancelledLogs = await dataService.GetAllCancelledAppointmentsForReportAsync(start, end);
                var explicitlyCancelled = cancelledLogs.Count;

                // No-show rule: an entry left over from a day that's already FULLY ended 
                var noShowPastDay = entries.Count(e => e.AppointmentDateTime.Date < today);

                report.CancelledAppointments = explicitlyCancelled + noShowPastDay;

                // Genuinely still pending — scheduled today or later, not yet resolved either way.
                report.PendingAppointments = entries.Count(e => e.AppointmentDateTime.Date >= today);

                report.TotalAppointments = report.CompletedAppointments + report.PendingAppointments + report.CancelledAppointments;

                // Names behind each count above, for the tap-to-view alert on the summary row — built once here so they work on every tab, not just Daily.
                report.CompletedAppointmentNames = billsInRange
                    .Select(b => string.IsNullOrWhiteSpace(b.PatientName) ? "—" : b.PatientName)
                    .ToList();
                report.PendingAppointmentNames = entries
                    .Where(e => e.AppointmentDateTime.Date >= today)
                    .Select(e => string.IsNullOrWhiteSpace(e.PatientName) ? "—" : e.PatientName)
                    .ToList();
                report.CancelledAppointmentNames = cancelledLogs
                    .Select(c => string.IsNullOrWhiteSpace(c.PatientName) ? "—" : c.PatientName)
                    .Concat(entries.Where(e => e.AppointmentDateTime.Date < today)
                        .Select(e => string.IsNullOrWhiteSpace(e.PatientName) ? "—" : e.PatientName))
                    .ToList();

                AppointmentChartData.Clear();
                AppointmentChartData.Add(new ChartDataPoint { Label = "Completed", Value = report.CompletedAppointments });
                AppointmentChartData.Add(new ChartDataPoint { Label = "Pending", Value = report.PendingAppointments });
                AppointmentChartData.Add(new ChartDataPoint { Label = "Cancelled", Value = report.CancelledAppointments });

                // ── Appointments table/list + insight — shape depends entirely on the selected tab ──
                TodayAppointmentRows.Clear();
                AppointmentWeeklyRows.Clear();
                AppointmentMonthlyRows.Clear();
                report.AppointmentsInsight = string.Empty;

                if (grouping == ReportPeriod.Daily)
                {
                    var rows = new List<TodayAppointmentRow>();

                    foreach (var bill in billsInRange)
                    {
                        rows.Add(new TodayAppointmentRow
                        {
                            SortTime = bill.CreatedAt,
                            TimeLabel = bill.CreatedAt.ToString("h:mm tt"),
                            PatientName = string.IsNullOrWhiteSpace(bill.PatientName) ? "—" : bill.PatientName,
                            StatusLabel = "Completed",
                            StatusColor = Color.FromArgb("#388E3C"),
                            StatusBgColor = Color.FromArgb("#E8F5E9")
                        });
                    }

                    foreach (var entry in entries)
                    {
                        bool isNoShow = entry.AppointmentDateTime.Date < today;

                        rows.Add(new TodayAppointmentRow
                        {
                            SortTime = entry.AppointmentDateTime,
                            TimeLabel = entry.AppointmentDateTime.ToString("h:mm tt"),
                            PatientName = string.IsNullOrWhiteSpace(entry.PatientName) ? "—" : entry.PatientName,
                            StatusLabel = isNoShow ? "No-show" : "Pending",
                            StatusColor = isNoShow ? Color.FromArgb("#D32F2F") : Color.FromArgb("#F57C00"),
                            StatusBgColor = isNoShow ? Color.FromArgb("#FCEAEA") : Color.FromArgb("#FFF3E0")
                        });
                    }

                    foreach (var cancelled in cancelledLogs)
                    {
                        rows.Add(new TodayAppointmentRow
                        {
                            SortTime = cancelled.AppointmentDateTime,
                            TimeLabel = cancelled.AppointmentDateTime.ToString("h:mm tt"),
                            PatientName = string.IsNullOrWhiteSpace(cancelled.PatientName) ? "—" : cancelled.PatientName,
                            StatusLabel = "Cancelled",
                            StatusColor = Color.FromArgb("#D32F2F"),
                            StatusBgColor = Color.FromArgb("#FCEAEA")
                        });
                    }

                    foreach (var row in rows.OrderBy(r => r.SortTime))
                        TodayAppointmentRows.Add(row);
                }
                else if (grouping == ReportPeriod.Weekly)
                {
                    // One row per clinic day (Mon–Sat) inside the selected week.
                    for (var day = start.Date; day < end.Date; day = day.AddDays(1))
                    {
                        if (day.DayOfWeek == DayOfWeek.Sunday) continue; // clinic closed

                        var (completed, cancelled) = GetDayAppointmentCounts(day, billsInRange, cancelledLogs, entries, today);
                        AppointmentWeeklyRows.Add(new AppointmentDayRow
                        {
                            Date = day,
                            DayLabel = $"{day:MMM d} ({day:ddd})",
                            Completed = completed,
                            Cancelled = cancelled
                        });
                    }

                    report.AppointmentsInsight = BuildBusiestDayInsight(
                        AppointmentWeeklyRows.Select(r => (r.Date.DayOfWeek.ToString(), r.Completed)),
                        "No appointments completed yet this week.");
                }
                else // Monthly-shaped (grouping == ReportPeriod.Monthly)
                {
                    // Aggregate every day in the month by WEEKDAY NAME
                    var weekdayTotals = new Dictionary<DayOfWeek, (int Completed, int Cancelled)>();

                    for (var day = start.Date; day < end.Date; day = day.AddDays(1))
                    {
                        if (day.DayOfWeek == DayOfWeek.Sunday) continue; // clinic closed

                        var (completed, cancelled) = GetDayAppointmentCounts(day, billsInRange, cancelledLogs, entries, today);
                        var existing = weekdayTotals.TryGetValue(day.DayOfWeek, out var v) ? v : (Completed: 0, Cancelled: 0);
                        weekdayTotals[day.DayOfWeek] = (existing.Completed + completed, existing.Cancelled + cancelled);
                    }

                    foreach (var dow in ClinicWeekdays)
                    {
                        var totals = weekdayTotals.TryGetValue(dow, out var v) ? v : (Completed: 0, Cancelled: 0);
                        AppointmentMonthlyRows.Add(new AppointmentWeekdayRow
                        {
                            DayLabel = dow.ToString(),
                            Completed = totals.Completed,
                            Cancelled = totals.Cancelled
                        });
                    }

                    report.AppointmentsInsight = BuildBusiestDayInsight(
                        AppointmentMonthlyRows.Select(r => (r.DayLabel, r.Completed)),
                        "No appointments completed yet this month.");
                }

                // ── TREATMENTS (condition-based) ──
                var treatments = await dataService.GetAllTreatmentHistoryForReportAsync(start, end);
                report.TotalTreatments = treatments.Count;
                report.TreatmentBreakdown = treatments
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.Condition) ? "Unspecified" : t.Condition)
                    .ToDictionary(g => g.Key, g => g.Count());
                report.MostCommonTreatment = report.TreatmentBreakdown.Count > 0
                    ? report.TreatmentBreakdown.OrderByDescending(kv => kv.Value).First().Key
                    : "—";

                // Top 10 only — the full breakdown can run long, and a chart/table with everything on it stops being readable on mobile
                var topTreatments = report.TreatmentBreakdown.OrderByDescending(kv => kv.Value).Take(10).ToList();

                TreatmentChartData.Clear();
                foreach (var kv in topTreatments)
                    TreatmentChartData.Add(new ChartDataPoint { Label = kv.Key, Value = kv.Value });

                // ~50dp per row is enough for a 2-line wrapped label; 240 is the floor so the card never looks squashed with few categories
                TreatmentsChartHeight = Math.Max(240, topTreatments.Count * 50);

                // ── Treatments table (Treatment | Count | % of Total) + insight — % is still of the period's grand total, not just the top 10 shown ──
                TreatmentTableRows.Clear();
                foreach (var kv in topTreatments)
                {
                    double pct = report.TotalTreatments > 0 ? (double)kv.Value / report.TotalTreatments * 100.0 : 0;
                    TreatmentTableRows.Add(new TreatmentRow { Treatment = kv.Key, Count = kv.Value, PercentOfTotal = pct });
                }

                report.TreatmentsInsight = BuildMostCommonTreatmentInsight(TreatmentTableRows);

                report.TotalRevenue = billsInRange.Sum(b => b.AmountPaid);
                report.OutstandingBalance = billsInRange.Sum(b => b.Balance);

                // Daily's chart is hidden (ShowBillingChart = false, set in SetPeriod) — no need to bucket revenue by hour anymore, just build the day-based series for Weekly/Monthly.
                BillingChartData.Clear();

                for (var day = start.Date; day < end.Date; day = day.AddDays(1))
                {
                    if (day.DayOfWeek == DayOfWeek.Sunday) continue; // clinic closed, no data possible

                    var dayEnd = day.AddDays(1);
                    var revenue = (double)allBills
                        .Where(b => b.VisitDate >= day && b.VisitDate < dayEnd)
                        .Sum(b => b.AmountPaid);
                    BillingChartData.Add(new ChartDataPoint { Label = day.ToString("MMM d"), Value = revenue });
                }

                // ── SERVICES RENDERED — counts each billed line item within this period ──
                var billIdsInRange = billsInRange.Select(b => b.Id).ToHashSet(); // bill IDs in range, used to filter items below
                var allBillItems = await dataService.GetAllBillItemsAsync();
                var itemsInRange = allBillItems.Where(i => billIdsInRange.Contains(i.BillId)).ToList();

                report.TotalServicesRendered = itemsInRange.Count;
                report.ServiceBreakdown = itemsInRange
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.ServiceName) ? "Unspecified" : i.ServiceName)
                    .ToDictionary(g => g.Key, g => g.Count());

                // ── "Top Services by Revenue" table (top 5) + Billing insight — the only Billing table now ──
                TopServiceRows.Clear();
                var topServices = itemsInRange
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.ServiceName) ? "Unspecified" : i.ServiceName)
                    .Select(g => new TopServiceRow
                    {
                        Service = g.Key,
                        TimesBilled = g.Count(),
                        Revenue = g.Sum(i => i.Subtotal)
                    })
                    .OrderByDescending(r => r.Revenue)
                    .Take(5)
                    .ToList();

                foreach (var row in topServices)
                    TopServiceRows.Add(row);

                report.BillingInsight = TopServiceRows.Count > 0
                    ? $"Top earner: {TopServiceRows[0].Service} — ₱{TopServiceRows[0].Revenue:N0}"
                    : "No billed services yet this period.";

                // ── SUPPLIES — reconstructed AS OF the end of the selected period, not just "right now" ──
                var supplies = await dataService.GetSuppliesAsync(); // today's live quantities — the starting point we work backward from
                report.TotalSupplies = supplies.Count;

                var logsAfterPeriod = await dataService.GetAllStockLogsForReportAsync(end, DateTime.MaxValue);

                var lowStockNames = new List<string>(); // names of items that were low as of that period, for the tap-to-view alert
                var outOfStockNames = new List<string>(); // names of items that were fully out as of that period
                var inStockNames = new List<string>(); // names of items that were sufficiently stocked as of that period
                int inStockAsOf = 0, lowAsOf = 0, outAsOf = 0; // per-item classification counters for that point in time

                foreach (var supply in supplies)
                {
                    // Sum of every change to THIS item that happened after the period ended
                    var changesSincePeriod = logsAfterPeriod
                        .Where(l => l.SupplyId == supply.Id)
                        .Sum(l => l.ChangeInPieces);

                    // Reverse those changes off today's quantity to get the historical quantity
                    var quantityAsOfPeriod = supply.QuantityInPieces - changesSincePeriod;

                    if (quantityAsOfPeriod <= 0)
                    {
                        outAsOf++;
                        outOfStockNames.Add(supply.Name);
                    }
                    else if (quantityAsOfPeriod <= supply.MinimumStockPieces)
                    {
                        lowAsOf++;
                        lowStockNames.Add(supply.Name);
                    }
                    else
                    {
                        inStockAsOf++;
                        inStockNames.Add(supply.Name);
                    }
                }

                report.OutOfStockCount = outAsOf;
                report.LowStockItemCount = lowAsOf;
                report.InStockCount = inStockAsOf;
                report.LowStockItemNames = lowStockNames;
                report.OutOfStockItemNames = outOfStockNames;
                report.InStockItemNames = inStockNames;

                SupplyChartData.Clear();
                SupplyChartData.Add(new ChartDataPoint { Label = "In Stock", Value = report.InStockCount });
                SupplyChartData.Add(new ChartDataPoint { Label = "Low Stock", Value = report.LowStockItemCount });
                SupplyChartData.Add(new ChartDataPoint { Label = "Out of Stock", Value = report.OutOfStockCount });

                // ── SUPPLIES — movement WITHIN this period (supply_stock_logs) ──
                var logs = await dataService.GetAllStockLogsForReportAsync(start, end); // logs that happened DURING the period, for the Restocked/Used totals
                report.PiecesRestocked = logs.Where(l => l.ChangeInPieces > 0).Sum(l => l.ChangeInPieces); // positive changes = stock added
                report.PiecesUsed = Math.Abs(logs.Where(l => l.ChangeInPieces < 0).Sum(l => l.ChangeInPieces)); // negative changes = stock consumed

                // ── Supplies table (Name | Used | Restocked, ranked by Used) + insight ──
                SupplyUsageRows.Clear();
                var supplyUsage = supplies
                    .Select(s =>
                    {
                        var supplyLogs = logs.Where(l => l.SupplyId == s.Id).ToList();
                        int used = Math.Abs(supplyLogs.Where(l => l.ChangeInPieces < 0).Sum(l => l.ChangeInPieces));
                        int restocked = supplyLogs.Where(l => l.ChangeInPieces > 0).Sum(l => l.ChangeInPieces);
                        return new SupplyUsageRow { Name = s.Name, Used = used, Restocked = restocked };
                    })
                    .Where(r => r.Used > 0 || r.Restocked > 0) // no movement this period — not worth a row
                    .OrderByDescending(r => r.Used)
                    .Take(5) // top 5 keeps this readable on mobile; the rest are still reflected in the summary totals above
                    .ToList();

                foreach (var row in supplyUsage)
                    SupplyUsageRows.Add(row);

                report.SuppliesInsight = SupplyUsageRows.Count > 0 && SupplyUsageRows[0].Used > 0
                    ? $"Most used: {SupplyUsageRows[0].Name} — {SupplyUsageRows[0].Used} used"
                    : "No supply usage recorded yet this period.";

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

        // Tapping the Completed/Pending/Cancelled count on the Appointments card lands here. Shows the specific patient names behind that count.
        [RelayCommand]
        async Task ShowAppointmentList(string status)
        {
            if (CurrentReport == null) return;

            var names = status switch
            {
                "Completed" => CurrentReport.CompletedAppointmentNames,
                "Pending" => CurrentReport.PendingAppointmentNames,
                "Cancelled" => CurrentReport.CancelledAppointmentNames,
                _ => new List<string>()
            };

            string message = names.Count > 0 ? string.Join("\n", names) : $"No {status.ToLower()} appointments for this period.";
            await Shell.Current.DisplayAlert($"{status} Appointments", message, "OK");
        }

        // Tapping the In Stock/Low/Out of Stock count on the Supplies card lands here. Shows the specific supply names behind that count.
        [RelayCommand]
        async Task ShowSupplyList(string status)
        {
            if (CurrentReport == null) return;

            var names = status switch
            {
                "In Stock" => CurrentReport.InStockItemNames,
                "Low Stock" => CurrentReport.LowStockItemNames,
                "Out of Stock" => CurrentReport.OutOfStockItemNames,
                _ => new List<string>()
            };

            string message = names.Count > 0 ? string.Join("\n", names) : $"No supplies are currently {status.ToLower()}.";
            await Shell.Current.DisplayAlert(status, message, "OK");
        }
    }
}
